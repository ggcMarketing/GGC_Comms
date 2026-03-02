using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;
using S7.Net;

namespace IndustrialGateway.Protocols.S7;

/// <summary>
/// Client implementation for Siemens S7 PLC communication using S7.NET+
/// </summary>
public class S7Client : IProtocolClient
{
    private readonly S7Config _config;
    private readonly ILogger<S7Client> _logger;
    private Plc? _plc;
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly object _lock = new();

    public ConnectionState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
        private set
        {
            lock (_lock)
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(this, value);
                }
            }
        }
    }

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<TagValue>? TagValueChanged;

    public S7Client(S7Config config, ILogger<S7Client> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (State == ConnectionState.Connected)
                {
                    _logger.LogWarning("Already connected to {Host}", _config.Host);
                    return;
                }

                State = ConnectionState.Connecting;

                // Map S7CpuType to S7.Net CpuType
                var cpuType = _config.CpuType switch
                {
                    S7CpuType.S7200 => CpuType.S7200,
                    S7CpuType.S7300 => CpuType.S7300,
                    S7CpuType.S7400 => CpuType.S7400,
                    S7CpuType.S71200 => CpuType.S71200,
                    S7CpuType.S71500 => CpuType.S71500,
                    _ => CpuType.S71200
                };

                _plc = new Plc(cpuType, _config.Host, _config.Rack, _config.Slot);
            }

            await Task.Run(() => _plc.Open(), cancellationToken);

            bool isConnected;
            lock (_lock)
            {
                isConnected = _plc.IsConnected;
            }

            if (isConnected)
            {
                State = ConnectionState.Connected;
                _logger.LogInformation("Connected to S7 PLC at {Host}:{Port}, Rack {Rack}, Slot {Slot}",
                    _config.Host, _config.Port, _config.Rack, _config.Slot);
            }
            else
            {
                State = ConnectionState.Error;
                throw new Exception("Failed to establish connection");
            }
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            _logger.LogError(ex, "Failed to connect to S7 PLC at {Host}", _config.Host);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            lock (_lock)
            {
                if (State == ConnectionState.Disconnected || _plc == null)
                {
                    return;
                }

                _plc.Close();
                State = ConnectionState.Disconnected;
            }

            _logger.LogInformation("Disconnected from S7 PLC at {Host}", _config.Host);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from S7 PLC");
            throw;
        }
    }

    public async Task<TagValue> ReadTagAsync(TagDefinition tag, CancellationToken cancellationToken = default)
    {
        try
        {
            if (State != ConnectionState.Connected || _plc == null)
            {
                throw new InvalidOperationException("Not connected to PLC");
            }

            object? value = await Task.Run(() => _plc.Read(tag.Address), cancellationToken);

            var tagValue = new TagValue
            {
                TagName = tag.Name,
                Value = value,
                Timestamp = DateTime.UtcNow,
                Quality = TagQuality.Good
            };

            TagValueChanged?.Invoke(this, tagValue);
            return tagValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read tag {TagName} at address {Address}", tag.Name, tag.Address);
            
            return new TagValue
            {
                TagName = tag.Name,
                Value = null,
                Timestamp = DateTime.UtcNow,
                Quality = TagQuality.Bad
            };
        }
    }

    public async Task<IEnumerable<TagValue>> ReadTagsAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        var results = new List<TagValue>();

        foreach (var tag in tags)
        {
            var result = await ReadTagAsync(tag, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public async Task WriteTagAsync(TagDefinition tag, object value, CancellationToken cancellationToken = default)
    {
        try
        {
            if (State != ConnectionState.Connected || _plc == null)
            {
                throw new InvalidOperationException("Not connected to PLC");
            }

            await Task.Run(() => _plc.Write(tag.Address, value), cancellationToken);

            _logger.LogInformation("Wrote value {Value} to tag {TagName} at address {Address}",
                value, tag.Name, tag.Address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write tag {TagName} at address {Address}", tag.Name, tag.Address);
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

    public Task SubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        // S7.NET+ doesn't support native subscriptions, would need polling implementation
        _logger.LogWarning("S7 protocol does not support native subscriptions. Use polling instead.");
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        // S7.NET+ doesn't support native subscriptions
        return Task.CompletedTask;
    }

    public Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        // S7.NET+ doesn't support native subscriptions
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            DisconnectAsync().Wait();
        }
        catch
        {
            // Ignore errors during disposal
        }
    }
}
