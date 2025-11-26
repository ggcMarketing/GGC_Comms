using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace IndustrialGateway.Protocols.Profinet;

/// <summary>
/// PROFINET protocol client (MVP STUB implementation)
///
/// NOTE: This is a simulated stub for MVP purposes. In production, you would use:
/// - Siemens PROFINET libraries (if licensed)
/// - Third-party PROFINET stacks
/// - Or implement PROFINET DCP/RT/IRT protocols from specification
///
/// This stub simulates realistic PROFINET behavior for testing the architecture.
/// </summary>
public class ProfinetClient : IProtocolClient
{
    private readonly ProfinetConfig _config;
    private readonly ILogger<ProfinetClient> _logger;
    private ConnectionState _state = ConnectionState.Disconnected;
    private CancellationTokenSource? _subscriptionCts;
    private Task? _subscriptionTask;
    private readonly List<TagDefinition> _subscribedTags = new();
    private readonly Dictionary<string, object> _simulatedData = new();
    private readonly Random _random = new();
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

    public ProfinetClient(ProfinetConfig config, ILogger<ProfinetClient> logger)
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
            _logger.LogInformation("[STUB] Connecting to PROFINET device '{DeviceName}' at {Host}, Rack {Rack}, Slot {Slot}",
                _config.DeviceName, _config.Host, _config.Rack, _config.Slot);

            // Simulate connection delay
            await Task.Delay(600, cancellationToken);

            // Simulate successful PROFINET connection and AR (Application Relation) establishment
            State = ConnectionState.Connected;
            _logger.LogInformation("[STUB] Successfully connected to PROFINET device (AR established)");
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            _logger.LogError(ex, "Failed to connect to PROFINET device");
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
            _logger.LogInformation("[STUB] Disconnecting from PROFINET device");

            await UnsubscribeAllAsync(cancellationToken);

            State = ConnectionState.Disconnected;
            _logger.LogInformation("[STUB] Disconnected from PROFINET device (AR released)");
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
            throw new InvalidOperationException("Not connected to PROFINET device");
        }

        try
        {
            // Simulate read delay (PROFINET is typically fast)
            await Task.Delay(20, cancellationToken);

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
            throw new InvalidOperationException("Not connected to PROFINET device");
        }

        try
        {
            // Simulate write delay
            await Task.Delay(20, cancellationToken);

            lock (_lockObject)
            {
                _simulatedData[tag.Address] = value;
            }

            _logger.LogInformation("[STUB] Successfully wrote value to tag: {TagName} = {Value}", tag.Name, value);
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
                    _logger.LogInformation("[STUB] Subscribed to tag: {TagName}", tag.Name);
                }
            }

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

    private async Task SubscriptionLoopAsync(CancellationToken cancellationToken)
    {
        var useRealTime = _config.UseRealTime ? "Real-Time (RT)" : "Non-RT";
        _logger.LogInformation("[STUB] Starting PROFINET subscription loop ({Mode})", useRealTime);

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
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogInformation("[STUB] PROFINET subscription loop stopped");
    }

    private object GetOrGenerateSimulatedValue(TagDefinition tag)
    {
        lock (_lockObject)
        {
            if (_simulatedData.TryGetValue(tag.Address, out var storedValue))
            {
                return storedValue;
            }

            return tag.DataType switch
            {
                TagDataType.Bool => _random.Next(0, 2) == 1,
                TagDataType.Int16 => (short)_random.Next(-2000, 2000),
                TagDataType.UInt16 => (ushort)_random.Next(0, 4000),
                TagDataType.Int32 => _random.Next(-20000, 20000),
                TagDataType.UInt32 => (uint)_random.Next(0, 40000),
                TagDataType.Float => (float)(_random.NextDouble() * 400 - 200),
                TagDataType.Double => _random.NextDouble() * 400 - 200,
                TagDataType.String => $"PN_Data_{_random.Next(1000, 9999)}",
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
