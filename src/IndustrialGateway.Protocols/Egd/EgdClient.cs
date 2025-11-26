using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace IndustrialGateway.Protocols.Egd;

/// <summary>
/// GE EGD (Ethernet Global Data) protocol client (MVP STUB implementation)
///
/// NOTE: This is a simulated stub for MVP purposes. In production, you would use:
/// - GE's proprietary libraries or reverse-engineered protocol implementations
/// - EGD is a UDP-based protocol for exchanging data between GE controllers
///
/// This stub simulates realistic EGD behavior for testing the architecture.
/// </summary>
public class EgdClient : IProtocolClient
{
    private readonly EgdConfig _config;
    private readonly ILogger<EgdClient> _logger;
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
            _logger.LogInformation("[STUB] Connecting to EGD device at {Host}:{Port}, Exchange ID {ExchangeId}",
                _config.Host, _config.Port, _config.ExchangeId);

            // Simulate connection delay
            await Task.Delay(300, cancellationToken);

            // EGD uses UDP, so connection is typically just setting up the socket
            State = ConnectionState.Connected;
            _logger.LogInformation("[STUB] Successfully configured EGD communication");
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
            _logger.LogInformation("[STUB] Disconnecting from EGD device");

            await UnsubscribeAllAsync(cancellationToken);

            State = ConnectionState.Disconnected;
            _logger.LogInformation("[STUB] Disconnected from EGD device");
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
            // Simulate write delay
            await Task.Delay(30, cancellationToken);

            // Store the written value in simulation
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
        _logger.LogInformation("[STUB] Starting EGD subscription loop (simulating UDP multicast)");

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

                // Use production interval from config
                await Task.Delay(_config.ProductionIntervalMs, cancellationToken);
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

        _logger.LogInformation("[STUB] EGD subscription loop stopped");
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
                TagDataType.Int16 => (short)_random.Next(-500, 500),
                TagDataType.UInt16 => (ushort)_random.Next(0, 1000),
                TagDataType.Int32 => _random.Next(-5000, 5000),
                TagDataType.UInt32 => (uint)_random.Next(0, 10000),
                TagDataType.Float => (float)(_random.NextDouble() * 100 - 50),
                TagDataType.Double => _random.NextDouble() * 100 - 50,
                TagDataType.String => $"EGD_Data_{_random.Next(100, 999)}",
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
