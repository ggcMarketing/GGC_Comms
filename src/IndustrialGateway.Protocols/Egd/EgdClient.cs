using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace IndustrialGateway.Protocols.Egd;

/// <summary>
/// GE EGD (Ethernet Global Data) protocol client
/// Supports both Producer (sending) and Consumer (receiving) modes
/// </summary>
public class EgdClient : IProtocolClient
{
    private readonly EgdConfig _config;
    private readonly ILogger<EgdClient> _logger;
    private ConnectionState _state = ConnectionState.Disconnected;
    private CancellationTokenSource? _subscriptionCts;
    private Task? _subscriptionTask;
    private Task? _producerTask;
    private readonly List<TagDefinition> _subscribedTags = new();
    private readonly Dictionary<string, object> _tagData = new();
    private readonly Random _random = new();
    private readonly object _lockObject = new();
    private UdpClient? _udpClient;
    private IPEndPoint? _remoteEndPoint;

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

    public EgdClient(EgdConfig config, ILogger<EgdClient> logger)
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
            _logger.LogInformation("Connecting to EGD - Mode: {Mode}, Host: {Host}:{Port}, Exchange ID: {ExchangeId}",
                _config.Mode, _config.Host, _config.Port, _config.ExchangeId);

            // Set up UDP client based on mode
            if (_config.Mode == EgdMode.Consumer || _config.Mode == EgdMode.Both)
            {
                // Consumer: Listen for incoming EGD packets
                _udpClient = new UdpClient(_config.Port);
                _logger.LogInformation("EGD Consumer listening on port {Port}", _config.Port);
            }
            else if (_config.Mode == EgdMode.Producer)
            {
                // Producer: Send EGD packets
                _udpClient = new UdpClient();
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_config.Host), _config.Port);
                _logger.LogInformation("EGD Producer configured to send to {Host}:{Port}", _config.Host, _config.Port);
            }

            State = ConnectionState.Connected;
            _logger.LogInformation("Successfully configured EGD communication");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            _logger.LogError(ex, "Failed to configure EGD communication");
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
            _logger.LogInformation("Disconnecting from EGD device");

            await UnsubscribeAllAsync(cancellationToken);

            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            State = ConnectionState.Disconnected;
            _logger.LogInformation("Disconnected from EGD device");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
            State = ConnectionState.Error;
        }
    }

    public async Task<TagValue> ReadTagAsync(TagDefinition tag, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to EGD device");
        }

        try
        {
            // Simulate read delay
            await Task.Delay(30, cancellationToken);

            // EGD is typically subscription-based, but we can simulate a read
            object value = GetOrGenerateSimulatedValue(tag);

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
        if (State != ConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to EGD device");
        }

        try
        {
            // Store the value for the producer to send
            lock (_lockObject)
            {
                _tagData[tag.Address] = value;
            }

            _logger.LogInformation("Updated tag value: {TagName} = {Value}", tag.Name, value);
            
            await Task.CompletedTask;
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

            if (_subscriptionTask == null || _subscriptionTask.IsCompleted)
            {
                _subscriptionCts = new CancellationTokenSource();
                
                // Start appropriate task based on mode
                if (_config.Mode == EgdMode.Consumer || _config.Mode == EgdMode.Both)
                {
                    _subscriptionTask = Task.Run(() => ConsumerLoopAsync(_subscriptionCts.Token), cancellationToken);
                }
                
                if (_config.Mode == EgdMode.Producer || _config.Mode == EgdMode.Both)
                {
                    _producerTask = Task.Run(() => ProducerLoopAsync(_subscriptionCts.Token), cancellationToken);
                }
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
                    _logger.LogInformation("[STUB] Unsubscribed from tag: {TagName}", tag.Name);
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
            catch (OperationCanceledException) { }
        }

        lock (_lockObject)
        {
            _subscribedTags.Clear();
        }

        _subscriptionCts?.Dispose();
        _subscriptionCts = null;
        _subscriptionTask = null;
    }

    private async Task ProducerLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting EGD Producer loop - sending to {Host}:{Port} every {Interval}ms",
            _config.Host, _config.Port, _config.ProductionIntervalMs);

        uint producerId = EgdProtocol.ProducerIdToUInt32(_config.ProducerId);

        while (!cancellationToken.IsCancellationRequested && State == ConnectionState.Connected)
        {
            try
            {
                // Build data payload from subscribed tags
                byte[] dataPayload = BuildDataPayload();

                // Build EGD packet
                byte[] packet = EgdProtocol.BuildEgdPacket((uint)_config.ExchangeId, producerId, dataPayload);

                // Send UDP packet
                if (_udpClient != null && _remoteEndPoint != null)
                {
                    await _udpClient.SendAsync(packet, packet.Length, _remoteEndPoint);
                    _logger.LogDebug("Sent EGD packet: {Bytes} bytes to {Endpoint}", packet.Length, _remoteEndPoint);
                }

                await Task.Delay(_config.ProductionIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in producer loop");
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogInformation("EGD Producer loop stopped");
    }

    private async Task ConsumerLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting EGD Consumer loop - listening on port {Port}", _config.Port);

        while (!cancellationToken.IsCancellationRequested && State == ConnectionState.Connected)
        {
            try
            {
                if (_udpClient == null)
                    break;

                // Receive UDP packet
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                byte[] receivedData = result.Buffer;

                // Parse EGD packet
                var (isValid, exchangeId, producerId, data) = EgdProtocol.ParseEgdPacket(receivedData);

                if (isValid && exchangeId == (uint)_config.ExchangeId)
                {
                    _logger.LogDebug("Received EGD packet: Exchange {ExchangeId}, Producer {ProducerId}, {Bytes} bytes",
                        exchangeId, producerId, data.Length);

                    // Parse data and update tag values
                    ParseDataPayload(data);
                }
                else if (!isValid)
                {
                    _logger.LogWarning("Received invalid EGD packet");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in consumer loop");
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogInformation("EGD Consumer loop stopped");
    }

    private byte[] BuildDataPayload()
    {
        lock (_lockObject)
        {
            // Simple implementation: pack tag data sequentially
            // In production, you'd follow the exact exchange format
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            foreach (var tag in _subscribedTags)
            {
                if (_tagData.TryGetValue(tag.Address, out var value))
                {
                    WriteTagValue(writer, tag.DataType, value);
                }
                else
                {
                    // Write default value
                    WriteTagValue(writer, tag.DataType, GetDefaultValue(tag.DataType));
                }
            }

            return ms.ToArray();
        }
    }

    private void ParseDataPayload(byte[] data)
    {
        lock (_lockObject)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            foreach (var tag in _subscribedTags)
            {
                try
                {
                    if (ms.Position >= ms.Length)
                        break;

                    object value = ReadTagValue(reader, tag.DataType);
                    _tagData[tag.Address] = value;

                    // Raise event for tag value change
                    var tagValue = new TagValue
                    {
                        TagId = tag.Id,
                        TagName = tag.Name,
                        Value = value,
                        Timestamp = DateTime.UtcNow,
                        Quality = TagQuality.Good
                    };

                    TagValueChanged?.Invoke(this, tagValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing tag {TagName}", tag.Name);
                }
            }
        }
    }

    private void WriteTagValue(BinaryWriter writer, TagDataType dataType, object value)
    {
        switch (dataType)
        {
            case TagDataType.Bool:
                writer.Write(Convert.ToBoolean(value));
                break;
            case TagDataType.Int16:
                writer.Write(Convert.ToInt16(value));
                break;
            case TagDataType.UInt16:
                writer.Write(Convert.ToUInt16(value));
                break;
            case TagDataType.Int32:
                writer.Write(Convert.ToInt32(value));
                break;
            case TagDataType.UInt32:
                writer.Write(Convert.ToUInt32(value));
                break;
            case TagDataType.Float:
                writer.Write(Convert.ToSingle(value));
                break;
            case TagDataType.Double:
                writer.Write(Convert.ToDouble(value));
                break;
            default:
                writer.Write(0);
                break;
        }
    }

    private object ReadTagValue(BinaryReader reader, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Bool => reader.ReadBoolean(),
            TagDataType.Int16 => reader.ReadInt16(),
            TagDataType.UInt16 => reader.ReadUInt16(),
            TagDataType.Int32 => reader.ReadInt32(),
            TagDataType.UInt32 => reader.ReadUInt32(),
            TagDataType.Float => reader.ReadSingle(),
            TagDataType.Double => reader.ReadDouble(),
            _ => 0
        };
    }

    private object GetDefaultValue(TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Bool => false,
            TagDataType.Int16 => (short)0,
            TagDataType.UInt16 => (ushort)0,
            TagDataType.Int32 => 0,
            TagDataType.UInt32 => 0U,
            TagDataType.Float => 0.0f,
            TagDataType.Double => 0.0,
            _ => 0
        };
    }

    private object GetOrGenerateSimulatedValue(TagDefinition tag)
    {
        lock (_lockObject)
        {
            if (_tagData.TryGetValue(tag.Address, out var storedValue))
            {
                return storedValue;
            }

            // Generate simulated value for testing
            return tag.DataType switch
            {
                TagDataType.Bool => _random.Next(0, 2) == 1,
                TagDataType.Int16 => (short)_random.Next(-500, 500),
                TagDataType.UInt16 => (ushort)_random.Next(0, 1000),
                TagDataType.Int32 => _random.Next(-5000, 5000),
                TagDataType.UInt32 => (uint)_random.Next(0, 10000),
                TagDataType.Float => (float)(_random.NextDouble() * 100 - 50),
                TagDataType.Double => _random.NextDouble() * 100 - 50,
                _ => 0
            };
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
