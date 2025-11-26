namespace IndustrialGateway.Core.Models;

/// <summary>
/// Configuration for MQTT connections
/// </summary>
public class MqttConfig : ConnectionConfig
{
    public override ProtocolType ProtocolType => ProtocolType.Mqtt;

    /// <summary>
    /// MQTT broker host
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// MQTT broker port (default 1883, 8883 for TLS)
    /// </summary>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// Client ID
    /// </summary>
    public string ClientId { get; set; } = $"IndustrialGateway_{Guid.NewGuid():N}";

    /// <summary>
    /// Username for authentication (optional)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication (optional)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Use TLS/SSL
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Clean session flag
    /// </summary>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// Keep alive period in seconds
    /// </summary>
    public int KeepAlivePeriodSeconds { get; set; } = 60;

    /// <summary>
    /// MQTT topics to subscribe to (for tags, topic is used as the address)
    /// </summary>
    public List<string> SubscribeTopics { get; set; } = new();

    /// <summary>
    /// QoS level (0, 1, 2)
    /// </summary>
    public int QosLevel { get; set; } = 1;
}
