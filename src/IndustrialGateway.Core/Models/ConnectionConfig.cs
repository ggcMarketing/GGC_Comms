using System.Text.Json.Serialization;

namespace IndustrialGateway.Core.Models;

/// <summary>
/// Base class for all protocol connection configurations
/// </summary>
[JsonDerivedType(typeof(ModbusTcpConfig), "ModbusTcp")]
[JsonDerivedType(typeof(EtherNetIpConfig), "EtherNetIp")]
[JsonDerivedType(typeof(EgdConfig), "Egd")]
[JsonDerivedType(typeof(S7Config), "S7")]
public abstract class ConnectionConfig
{
    /// <summary>
    /// Unique identifier for this connection
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User-friendly connection name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Protocol type
    /// </summary>
    public abstract ProtocolType ProtocolType { get; }

    /// <summary>
    /// Whether this connection is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Tags/data points to read from this connection
    /// </summary>
    public List<TagDefinition> Tags { get; set; } = new();

    /// <summary>
    /// Default scan rate in milliseconds
    /// </summary>
    public int DefaultScanRateMs { get; set; } = 1000;
}

/// <summary>
/// Protocol types supported by the gateway
/// </summary>
public enum ProtocolType
{
    ModbusTcp,
    EtherNetIp,
    Egd,
    S7
}
