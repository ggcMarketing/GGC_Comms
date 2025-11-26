using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace IndustrialGateway.Protocols.OpcUa;

/// <summary>
/// OPC UA protocol client implementation using OPC Foundation library
/// </summary>
public class OpcUaClient : IProtocolClient
{
    private readonly OpcUaConfig _config;
    private readonly ILogger<OpcUaClient> _logger;
    private ApplicationInstance? _application;
    private Session? _session;
    private Subscription? _subscription;
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly object _lockObject = new();
    private readonly Dictionary<string, MonitoredItem> _monitoredItems = new();

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

    public OpcUaClient(OpcUaConfig config, ILogger<OpcUaClient> logger)
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
            _logger.LogInformation("Connecting to OPC UA server at {EndpointUrl}", _config.EndpointUrl);

            // Create application configuration
            var config = new ApplicationConfiguration
            {
                ApplicationName = _config.ApplicationName,
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false
                },
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = _config.SessionTimeoutMs
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = _config.SessionTimeoutMs
                },
                TraceConfiguration = new TraceConfiguration
                {
                    TraceMasks = 0 // Disable tracing
                }
            };

            await config.Validate(ApplicationType.Client);

            _application = new ApplicationInstance
            {
                ApplicationConfiguration = config
            };

            // Check certificate
            bool haveAppCertificate = await _application.CheckApplicationInstanceCertificate(false, 0);

            // Discover endpoints
            var endpointUrl = _config.EndpointUrl;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, false);

            // Create session
            var userIdentity = string.IsNullOrEmpty(_config.Username)
                ? new UserIdentity(new AnonymousIdentityToken())
                : new UserIdentity(_config.Username, _config.Password ?? string.Empty);

            _session = await Session.Create(
                config,
                new ConfiguredEndpoint(null, endpoint, endpointConfiguration),
                false,
                _config.ApplicationName,
                (uint)_config.SessionTimeoutMs,
                userIdentity,
                null);

            _session.KeepAlive += Session_KeepAlive;

            State = ConnectionState.Connected;
            _logger.LogInformation("Successfully connected to OPC UA server");
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            _logger.LogError(ex, "Failed to connect to OPC UA server");
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
            _logger.LogInformation("Disconnecting from OPC UA server");

            // Unsubscribe all
            await UnsubscribeAllAsync(cancellationToken);

            // Close session
            if (_session != null)
            {
                _session.KeepAlive -= Session_KeepAlive;
                _session.Close();
                _session.Dispose();
                _session = null;
            }

            State = ConnectionState.Disconnected;
            _logger.LogInformation("Disconnected from OPC UA server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
            State = ConnectionState.Error;
        }
    }

    public async Task<TagValue> ReadTagAsync(TagDefinition tag, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _session == null)
        {
            throw new InvalidOperationException("Not connected to OPC UA server");
        }

        try
        {
            var nodeId = NodeId.Parse(tag.Address);
            var value = _session.ReadValue(nodeId);

            return new TagValue
            {
                TagId = tag.Id,
                TagName = tag.Name,
                Value = value.Value,
                Timestamp = value.ServerTimestamp,
                Quality = ConvertStatusCode(value.StatusCode)
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
        if (State != ConnectionState.Connected || _session == null)
        {
            throw new InvalidOperationException("Not connected to OPC UA server");
        }

        try
        {
            var nodeIds = new ReadValueIdCollection();
            var tagList = tags.ToList();

            foreach (var tag in tagList)
            {
                nodeIds.Add(new ReadValueId
                {
                    NodeId = NodeId.Parse(tag.Address),
                    AttributeId = Attributes.Value
                });
            }

            _session.Read(
                null,
                0,
                TimestampsToReturn.Both,
                nodeIds,
                out var results,
                out var diagnosticInfos);

            var tagValues = new List<TagValue>();
            for (int i = 0; i < tagList.Count; i++)
            {
                var tag = tagList[i];
                var result = results[i];

                tagValues.Add(new TagValue
                {
                    TagId = tag.Id,
                    TagName = tag.Name,
                    Value = result.Value,
                    Timestamp = result.ServerTimestamp,
                    Quality = ConvertStatusCode(result.StatusCode)
                });
            }

            return tagValues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading tags");
            throw;
        }
    }

    public async Task WriteTagAsync(TagDefinition tag, object value, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _session == null)
        {
            throw new InvalidOperationException("Not connected to OPC UA server");
        }

        try
        {
            var nodeId = NodeId.Parse(tag.Address);
            var writeValue = new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            var writeValues = new WriteValueCollection { writeValue };

            _session.Write(
                null,
                writeValues,
                out var results,
                out var diagnosticInfos);

            if (StatusCode.IsBad(results[0]))
            {
                throw new InvalidOperationException($"Write failed with status: {results[0]}");
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
        if (State != ConnectionState.Connected || _session == null)
        {
            throw new InvalidOperationException("Not connected to OPC UA server");
        }

        try
        {
            var writeValues = new WriteValueCollection();

            foreach (var kvp in tagValues)
            {
                writeValues.Add(new WriteValue
                {
                    NodeId = NodeId.Parse(kvp.Key.Address),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(kvp.Value))
                });
            }

            _session.Write(
                null,
                writeValues,
                out var results,
                out var diagnosticInfos);

            for (int i = 0; i < results.Count; i++)
            {
                if (StatusCode.IsBad(results[i]))
                {
                    _logger.LogWarning("Write failed for tag with status: {Status}", results[i]);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing tags");
            throw;
        }
    }

    public async Task SubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _session == null)
        {
            throw new InvalidOperationException("Not connected to OPC UA server");
        }

        lock (_lockObject)
        {
            // Create subscription if it doesn't exist
            if (_subscription == null)
            {
                _subscription = new Subscription(_session.DefaultSubscription)
                {
                    PublishingEnabled = true,
                    PublishingInterval = _config.DefaultScanRateMs,
                    KeepAliveCount = 10,
                    LifetimeCount = 100,
                    MaxNotificationsPerPublish = 1000,
                    Priority = 100
                };

                _session.AddSubscription(_subscription);
                _subscription.Create();
            }

            foreach (var tag in tags)
            {
                if (!_monitoredItems.ContainsKey(tag.Id))
                {
                    var monitoredItem = new MonitoredItem(_subscription.DefaultItem)
                    {
                        StartNodeId = NodeId.Parse(tag.Address),
                        AttributeId = Attributes.Value,
                        DisplayName = tag.Name,
                        SamplingInterval = tag.ScanRateMs
                    };

                    monitoredItem.Notification += (item, e) => MonitoredItem_Notification(item, e, tag);

                    _subscription.AddItem(monitoredItem);
                    _monitoredItems[tag.Id] = monitoredItem;

                    _logger.LogInformation("Subscribed to tag: {TagName}", tag.Name);
                }
            }

            _subscription.ApplyChanges();
        }

        await Task.CompletedTask;
    }

    public async Task UnsubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        lock (_lockObject)
        {
            foreach (var tag in tags)
            {
                if (_monitoredItems.TryGetValue(tag.Id, out var monitoredItem))
                {
                    _subscription?.RemoveItem(monitoredItem);
                    _monitoredItems.Remove(tag.Id);
                    _logger.LogInformation("Unsubscribed from tag: {TagName}", tag.Name);
                }
            }

            _subscription?.ApplyChanges();
        }

        await Task.CompletedTask;
    }

    public async Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lockObject)
        {
            if (_subscription != null)
            {
                _session?.RemoveSubscription(_subscription);
                _subscription.Delete(true);
                _subscription.Dispose();
                _subscription = null;
            }

            _monitoredItems.Clear();
        }

        await Task.CompletedTask;
    }

    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (ServiceResult.IsBad(e.Status))
        {
            _logger.LogWarning("Keep-alive failed: {Status}", e.Status);
            State = ConnectionState.Error;
        }
    }

    private void MonitoredItem_Notification(MonitoredItem item, MonitoredItemNotificationEventArgs e, TagDefinition tag)
    {
        try
        {
            if (e.NotificationValue is MonitoredItemNotification notification)
            {
                var tagValue = new TagValue
                {
                    TagId = tag.Id,
                    TagName = tag.Name,
                    Value = notification.Value.Value,
                    Timestamp = notification.Value.ServerTimestamp,
                    Quality = ConvertStatusCode(notification.Value.StatusCode)
                };

                TagValueChanged?.Invoke(this, tagValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification for tag: {TagName}", tag.Name);
        }
    }

    private static TagQuality ConvertStatusCode(StatusCode statusCode)
    {
        if (StatusCode.IsGood(statusCode))
            return TagQuality.Good;
        if (StatusCode.IsUncertain(statusCode))
            return TagQuality.Uncertain;
        return TagQuality.Bad;
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
