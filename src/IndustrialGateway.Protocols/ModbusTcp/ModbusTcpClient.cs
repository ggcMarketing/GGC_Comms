using System.Net.Sockets;
using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;
using NModbus;

namespace IndustrialGateway.Protocols.ModbusTcp;

/// <summary>
/// Modbus TCP protocol client implementation using NModbus library
/// </summary>
public class ModbusTcpClient : IProtocolClient
{
    private readonly ModbusTcpConfig _config;
    private readonly ILogger<ModbusTcpClient> _logger;
    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private ConnectionState _state = ConnectionState.Disconnected;
    private CancellationTokenSource? _subscriptionCts;
    private Task? _subscriptionTask;
    private readonly List<TagDefinition> _subscribedTags = new();
    private readonly object _lockObject = new();

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(this, value);
                _logger.LogInformation("Connection state changed to: {State}", value);
            }
        }
    }

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<TagValue>? TagValueChanged;

    public ModbusTcpClient(ModbusTcpConfig config, ILogger<ModbusTcpClient> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == ConnectionState.Connected)
        {
            _logger.LogWarning("Already connected");
            return;
        }

        try
        {
            State = ConnectionState.Connecting;
            _logger.LogInformation("Connecting to Modbus TCP server at {Host}:{Port}", _config.Host, _config.Port);

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_config.Host, _config.Port, cancellationToken);

            var factory = new ModbusFactory();
            _modbusMaster = factory.CreateMaster(_tcpClient);

            // Set timeout
            _tcpClient.SendTimeout = _config.TimeoutMs;
            _tcpClient.ReceiveTimeout = _config.TimeoutMs;

            State = ConnectionState.Connected;
            _logger.LogInformation("Successfully connected to Modbus TCP server");
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            _logger.LogError(ex, "Failed to connect to Modbus TCP server");
            await DisconnectAsync(cancellationToken);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == ConnectionState.Disconnected)
        {
            return;
        }

        try
        {
            State = ConnectionState.Disconnecting;
            _logger.LogInformation("Disconnecting from Modbus TCP server");

            // Stop subscriptions
            await UnsubscribeAllAsync(cancellationToken);

            // Dispose Modbus master
            _modbusMaster?.Dispose();
            _modbusMaster = null;

            // Close TCP client
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;

            State = ConnectionState.Disconnected;
            _logger.LogInformation("Disconnected from Modbus TCP server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
            State = ConnectionState.Error;
        }
    }

    public async Task<TagValue> ReadTagAsync(TagDefinition tag, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _modbusMaster == null)
        {
            throw new InvalidOperationException("Not connected to Modbus server");
        }

        try
        {
            // Parse address - expect format like "40001" (holding register) or "30001" (input register)
            // or "10001" (coil) or "00001" (discrete input)
            if (!ushort.TryParse(tag.Address, out var address))
            {
                throw new ArgumentException($"Invalid Modbus address: {tag.Address}");
            }

            object? value = null;

            // Determine register type based on address range (Modbus addressing convention)
            if (address >= 40001 && address <= 49999) // Holding registers
            {
                address = (ushort)(address - 40001);
                value = await ReadHoldingRegisterValueAsync(address, tag.DataType, cancellationToken);
            }
            else if (address >= 30001 && address <= 39999) // Input registers
            {
                address = (ushort)(address - 30001);
                value = await ReadInputRegisterValueAsync(address, tag.DataType, cancellationToken);
            }
            else if (address >= 10001 && address <= 19999) // Coils
            {
                address = (ushort)(address - 10001);
                var coils = await Task.Run(() => _modbusMaster.ReadCoils(_config.SlaveId, address, 1), cancellationToken);
                value = coils[0];
            }
            else if (address >= 1 && address <= 9999) // Discrete inputs
            {
                address = (ushort)(address - 1);
                var inputs = await Task.Run(() => _modbusMaster.ReadInputs(_config.SlaveId, address, 1), cancellationToken);
                value = inputs[0];
            }
            else
            {
                throw new ArgumentException($"Address out of range: {tag.Address}");
            }

            return new TagValue
            {
                TagId = tag.Id,
                TagName = tag.Name,
                Value = value,
                Timestamp = DateTime.UtcNow,
                Quality = TagQuality.Good
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading tag: {TagName}", tag.Name);
            return new TagValue
            {
                TagId = tag.Id,
                TagName = tag.Name,
                Value = null,
                Timestamp = DateTime.UtcNow,
                Quality = TagQuality.Bad,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IEnumerable<TagValue>> ReadTagsAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        var results = new List<TagValue>();
        foreach (var tag in tags)
        {
            results.Add(await ReadTagAsync(tag, cancellationToken));
        }
        return results;
    }

    public async Task WriteTagAsync(TagDefinition tag, object value, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _modbusMaster == null)
        {
            throw new InvalidOperationException("Not connected to Modbus server");
        }

        if (!ushort.TryParse(tag.Address, out var address))
        {
            throw new ArgumentException($"Invalid Modbus address: {tag.Address}");
        }

        try
        {
            // Holding registers only (40001-49999)
            if (address >= 40001 && address <= 49999)
            {
                address = (ushort)(address - 40001);
                await WriteHoldingRegisterValueAsync(address, tag.DataType, value, cancellationToken);
            }
            // Coils (10001-19999)
            else if (address >= 10001 && address <= 19999)
            {
                address = (ushort)(address - 10001);
                bool boolValue = Convert.ToBoolean(value);
                await Task.Run(() => _modbusMaster.WriteSingleCoil(_config.SlaveId, address, boolValue), cancellationToken);
            }
            else
            {
                throw new ArgumentException($"Cannot write to address: {tag.Address}. Only holding registers (40001+) and coils (10001+) are writable.");
            }

            _logger.LogInformation("Successfully wrote value to tag: {TagName}", tag.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing tag: {TagName}", tag.Name);
            throw;
        }
    }

    public async Task WriteTagsAsync(IDictionary<TagDefinition, object> tagValues, CancellationToken cancellationToken = default)
    {
        foreach (var kvp in tagValues)
        {
            await WriteTagAsync(kvp.Key, kvp.Value, cancellationToken);
        }
    }

    public async Task SubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        lock (_lockObject)
        {
            foreach (var tag in tags)
            {
                if (!_subscribedTags.Any(t => t.Id == tag.Id))
                {
                    _subscribedTags.Add(tag);
                    _logger.LogInformation("Subscribed to tag: {TagName}", tag.Name);
                }
            }

            // Start subscription task if not already running
            if (_subscriptionTask == null || _subscriptionTask.IsCompleted)
            {
                _subscriptionCts = new CancellationTokenSource();
                _subscriptionTask = Task.Run(() => SubscriptionLoopAsync(_subscriptionCts.Token), cancellationToken);
            }
        }

        await Task.CompletedTask;
    }

    public async Task UnsubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        lock (_lockObject)
        {
            foreach (var tag in tags)
            {
                var existing = _subscribedTags.FirstOrDefault(t => t.Id == tag.Id);
                if (existing != null)
                {
                    _subscribedTags.Remove(existing);
                    _logger.LogInformation("Unsubscribed from tag: {TagName}", tag.Name);
                }
            }
        }

        await Task.CompletedTask;
    }

    public async Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        _subscriptionCts?.Cancel();

        if (_subscriptionTask != null)
        {
            try
            {
                await _subscriptionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        lock (_lockObject)
        {
            _subscribedTags.Clear();
        }

        _subscriptionCts?.Dispose();
        _subscriptionCts = null;
        _subscriptionTask = null;
    }

    private async Task SubscriptionLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting subscription loop");

        while (!cancellationToken.IsCancellationRequested && State == ConnectionState.Connected)
        {
            try
            {
                List<TagDefinition> tagsToRead;
                lock (_lockObject)
                {
                    tagsToRead = _subscribedTags.ToList();
                }

                foreach (var tag in tagsToRead)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var tagValue = await ReadTagAsync(tag, cancellationToken);
                    TagValueChanged?.Invoke(this, tagValue);
                }

                // Use the minimum scan rate from subscribed tags
                var minScanRate = tagsToRead.Any()
                    ? tagsToRead.Min(t => t.ScanRateMs)
                    : _config.DefaultScanRateMs;

                await Task.Delay(minScanRate, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in subscription loop");
                await Task.Delay(1000, cancellationToken); // Back off on error
            }
        }

        _logger.LogInformation("Subscription loop stopped");
    }

    private async Task<object> ReadHoldingRegisterValueAsync(ushort address, TagDataType dataType, CancellationToken cancellationToken)
    {
        return dataType switch
        {
            TagDataType.Bool => (await Task.Run(() => _modbusMaster!.ReadHoldingRegisters(_config.SlaveId, address, 1), cancellationToken))[0] != 0,
            TagDataType.Int16 => (short)(await Task.Run(() => _modbusMaster!.ReadHoldingRegisters(_config.SlaveId, address, 1), cancellationToken))[0],
            TagDataType.UInt16 => (await Task.Run(() => _modbusMaster!.ReadHoldingRegisters(_config.SlaveId, address, 1), cancellationToken))[0],
            TagDataType.Int32 => await ReadInt32Async(address, cancellationToken),
            TagDataType.UInt32 => await ReadUInt32Async(address, cancellationToken),
            TagDataType.Float => await ReadFloatAsync(address, cancellationToken),
            _ => (await Task.Run(() => _modbusMaster!.ReadHoldingRegisters(_config.SlaveId, address, 1), cancellationToken))[0]
        };
    }

    private async Task<object> ReadInputRegisterValueAsync(ushort address, TagDataType dataType, CancellationToken cancellationToken)
    {
        return dataType switch
        {
            TagDataType.Bool => (await Task.Run(() => _modbusMaster!.ReadInputRegisters(_config.SlaveId, address, 1), cancellationToken))[0] != 0,
            TagDataType.Int16 => (short)(await Task.Run(() => _modbusMaster!.ReadInputRegisters(_config.SlaveId, address, 1), cancellationToken))[0],
            TagDataType.UInt16 => (await Task.Run(() => _modbusMaster!.ReadInputRegisters(_config.SlaveId, address, 1), cancellationToken))[0],
            _ => (await Task.Run(() => _modbusMaster!.ReadInputRegisters(_config.SlaveId, address, 1), cancellationToken))[0]
        };
    }

    private async Task<int> ReadInt32Async(ushort address, CancellationToken cancellationToken)
    {
        var registers = await Task.Run(() => _modbusMaster!.ReadHoldingRegisters(_config.SlaveId, address, 2), cancellationToken);
        return (registers[0] << 16) | registers[1];
    }

    private async Task<uint> ReadUInt32Async(ushort address, CancellationToken cancellationToken)
    {
        var registers = await Task.Run(() => _modbusMaster!.ReadHoldingRegisters(_config.SlaveId, address, 2), cancellationToken);
        return ((uint)registers[0] << 16) | registers[1];
    }

    private async Task<float> ReadFloatAsync(ushort address, CancellationToken cancellationToken)
    {
        var registers = await Task.Run(() => _modbusMaster!.ReadHoldingRegisters(_config.SlaveId, address, 2), cancellationToken);
        var bytes = new byte[4];
        bytes[0] = (byte)(registers[1] & 0xFF);
        bytes[1] = (byte)(registers[1] >> 8);
        bytes[2] = (byte)(registers[0] & 0xFF);
        bytes[3] = (byte)(registers[0] >> 8);
        return BitConverter.ToSingle(bytes, 0);
    }

    private async Task WriteHoldingRegisterValueAsync(ushort address, TagDataType dataType, object value, CancellationToken cancellationToken)
    {
        switch (dataType)
        {
            case TagDataType.Bool:
                await Task.Run(() => _modbusMaster!.WriteSingleRegister(_config.SlaveId, address, (ushort)(Convert.ToBoolean(value) ? 1 : 0)), cancellationToken);
                break;
            case TagDataType.Int16:
                await Task.Run(() => _modbusMaster!.WriteSingleRegister(_config.SlaveId, address, (ushort)(short)Convert.ToInt16(value)), cancellationToken);
                break;
            case TagDataType.UInt16:
                await Task.Run(() => _modbusMaster!.WriteSingleRegister(_config.SlaveId, address, Convert.ToUInt16(value)), cancellationToken);
                break;
            case TagDataType.Int32:
                {
                    var intValue = Convert.ToInt32(value);
                    var registers = new ushort[] { (ushort)(intValue >> 16), (ushort)(intValue & 0xFFFF) };
                    await Task.Run(() => _modbusMaster!.WriteMultipleRegisters(_config.SlaveId, address, registers), cancellationToken);
                    break;
                }
            case TagDataType.UInt32:
                {
                    var uintValue = Convert.ToUInt32(value);
                    var registers = new ushort[] { (ushort)(uintValue >> 16), (ushort)(uintValue & 0xFFFF) };
                    await Task.Run(() => _modbusMaster!.WriteMultipleRegisters(_config.SlaveId, address, registers), cancellationToken);
                    break;
                }
            case TagDataType.Float:
                {
                    var floatValue = Convert.ToSingle(value);
                    var bytes = BitConverter.GetBytes(floatValue);
                    var registers = new ushort[] {
                        (ushort)((bytes[3] << 8) | bytes[2]),
                        (ushort)((bytes[1] << 8) | bytes[0])
                    };
                    await Task.Run(() => _modbusMaster!.WriteMultipleRegisters(_config.SlaveId, address, registers), cancellationToken);
                    break;
                }
            default:
                await Task.Run(() => _modbusMaster!.WriteSingleRegister(_config.SlaveId, address, Convert.ToUInt16(value)), cancellationToken);
                break;
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
