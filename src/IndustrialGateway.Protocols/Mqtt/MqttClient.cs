using System.Text;
using System.Text.Json;
using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

namespace IndustrialGateway.Protocols.Mqtt;

/// <summary>
/// MQTT protocol client implementation using MQTTnet library
/// </summary>
public class MqttClient : IProtocolClient
{
    private readonly MqttConfig _config;
    private readonly ILogger<MqttClient> _logger;
    private IMqttClient? _mqttClient;
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly Dictionary<string, TagDefinition> _subscribedTags = new();
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

    public MqttClient(MqttConfig config, ILogger<MqttClient> logger)
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
            _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port}", _config.Host, _config.Port);

            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // Configure options
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.Host, _config.Port)
                .WithClientId(_config.ClientId)
                .WithCleanSession(_config.CleanSession)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_config.KeepAlivePeriodSeconds));

            if (!string.IsNullOrEmpty(_config.Username))
            {
                optionsBuilder.WithCredentials(_config.Username, _config.Password);
            }

            if (_config.UseTls)
            {
                optionsBuilder.WithTls();
            }

            var options = optionsBuilder.Build();

            // Set up event handlers
            _mqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            _mqttClient.DisconnectedAsync += MqttClient_DisconnectedAsync;

            // Connect
            var result = await _mqttClient.ConnectAsync(options, cancellationToken);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                State = ConnectionState.Connected;
                _logger.LogInformation("Successfully connected to MQTT broker");

                // Subscribe to configured topics
                if (_config.SubscribeTopics.Any())
                {
                    await SubscribeToTopicsAsync(_config.SubscribeTopics, cancellationToken);
                }
            }
            else
            {
                throw new InvalidOperationException($"MQTT connection failed: {result.ResultCode} - {result.ReasonString}");
            }
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            _logger.LogError(ex, "Failed to connect to MQTT broker");
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
            _logger.LogInformation("Disconnecting from MQTT broker");

            if (_mqttClient != null)
            {
                await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
                _mqttClient.Dispose();
                _mqttClient = null;
            }

            lock (_lockObject)
            {
                _subscribedTags.Clear();
            }

            State = ConnectionState.Disconnected;
            _logger.LogInformation("Disconnected from MQTT broker");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
            State = ConnectionState.Error;
        }
    }

    public async Task<TagValue> ReadTagAsync(TagDefinition tag, CancellationToken cancellationToken = default)
    {
        // MQTT is publish/subscribe, so direct read is not typical
        // We'll return the last received value if subscribed, or indicate it's not available
        _logger.LogWarning("MQTT does not support direct read operations. Use subscription instead for tag: {TagName}", tag.Name);

        return new TagValue
        {
            TagId = tag.Id,
            TagName = tag.Name,
            Value = null,
            Timestamp = DateTime.UtcNow,
            Quality = TagQuality.Uncertain,
            ErrorMessage = "MQTT does not support direct read. Use subscription."
        };
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
        if (State != ConnectionState.Connected || _mqttClient == null)
        {
            throw new InvalidOperationException("Not connected to MQTT broker");
        }

        try
        {
            // Use the tag address as the MQTT topic
            var topic = tag.Address;
            var payload = ConvertValueToPayload(value);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)_config.QosLevel)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(message, cancellationToken);

            _logger.LogInformation("Published to topic {Topic}: {Payload}", topic, payload);
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
        if (State != ConnectionState.Connected || _mqttClient == null)
        {
            throw new InvalidOperationException("Not connected to MQTT broker");
        }

        var topics = new List<string>();

        lock (_lockObject)
        {
            foreach (var tag in tags)
            {
                if (!_subscribedTags.ContainsKey(tag.Id))
                {
                    _subscribedTags[tag.Id] = tag;
                    topics.Add(tag.Address); // Use tag address as MQTT topic
                }
            }
        }

        if (topics.Any())
        {
            await SubscribeToTopicsAsync(topics, cancellationToken);
        }
    }

    public async Task UnsubscribeAsync(IEnumerable<TagDefinition> tags, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _mqttClient == null)
        {
            throw new InvalidOperationException("Not connected to MQTT broker");
        }

        var topics = new List<string>();

        lock (_lockObject)
        {
            foreach (var tag in tags)
            {
                if (_subscribedTags.TryGetValue(tag.Id, out var subscribedTag))
                {
                    topics.Add(subscribedTag.Address);
                    _subscribedTags.Remove(tag.Id);
                }
            }
        }

        if (topics.Any())
        {
            await _mqttClient.UnsubscribeAsync(topics, cancellationToken);
            _logger.LogInformation("Unsubscribed from topics: {Topics}", string.Join(", ", topics));
        }
    }

    public async Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        if (_mqttClient == null)
        {
            return;
        }

        List<string> topics;
        lock (_lockObject)
        {
            topics = _subscribedTags.Values.Select(t => t.Address).ToList();
            _subscribedTags.Clear();
        }

        if (topics.Any() && State == ConnectionState.Connected)
        {
            await _mqttClient.UnsubscribeAsync(topics, cancellationToken);
            _logger.LogInformation("Unsubscribed from all topics");
        }
    }

    private async Task SubscribeToTopicsAsync(IEnumerable<string> topics, CancellationToken cancellationToken)
    {
        if (_mqttClient == null)
        {
            return;
        }

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder();

        foreach (var topic in topics)
        {
            subscribeOptions.WithTopicFilter(
                f => f.WithTopic(topic)
                      .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)_config.QosLevel));
        }

        var result = await _mqttClient.SubscribeAsync(subscribeOptions.Build(), cancellationToken);

        _logger.LogInformation("Subscribed to topics: {Topics}", string.Join(", ", topics));
    }

    private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            // Find tag by topic (address)
            TagDefinition? tag;
            lock (_lockObject)
            {
                tag = _subscribedTags.Values.FirstOrDefault(t => t.Address == topic);
            }

            if (tag != null)
            {
                var value = ParsePayload(payload, tag.DataType);

                var tagValue = new TagValue
                {
                    TagId = tag.Id,
                    TagName = tag.Name,
                    Value = value,
                    Timestamp = DateTime.UtcNow,
                    Quality = TagQuality.Good
                };

                TagValueChanged?.Invoke(this, tagValue);

                _logger.LogDebug("Received message on topic {Topic}: {Payload}", topic, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message");
        }

        return Task.CompletedTask;
    }

    private Task MqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("Disconnected from MQTT broker: {Reason}", e.Reason);
        State = ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    private static string ConvertValueToPayload(object value)
    {
        if (value is string str)
        {
            return str;
        }

        // Try to serialize as JSON for complex types
        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    private static object? ParsePayload(string payload, TagDataType dataType)
    {
        try
        {
            return dataType switch
            {
                TagDataType.Bool => bool.Parse(payload),
                TagDataType.Int16 => short.Parse(payload),
                TagDataType.UInt16 => ushort.Parse(payload),
                TagDataType.Int32 => int.Parse(payload),
                TagDataType.UInt32 => uint.Parse(payload),
                TagDataType.Int64 => long.Parse(payload),
                TagDataType.UInt64 => ulong.Parse(payload),
                TagDataType.Float => float.Parse(payload),
                TagDataType.Double => double.Parse(payload),
                TagDataType.String => payload,
                _ => payload
            };
        }
        catch
        {
            return payload; // Return as string if parsing fails
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
