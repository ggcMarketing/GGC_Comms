namespace IndustrialGateway.Core.Models;

/// <summary>
/// Configuration for GE EGD (Ethernet Global Data) connections
/// </summary>
public class EgdConfig : ConnectionConfig
{
    public override ProtocolType ProtocolType => ProtocolType.Egd;

    /// <summary>
    /// IP address or hostname of the EGD device
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// UDP port for EGD (typically 18246)
    /// </summary>
    public int Port { get; set; } = 18246;

    /// <summary>
    /// Exchange ID for this connection
    /// </summary>
    public int ExchangeId { get; set; } = 1;

    /// <summary>
    /// Producer ID (source of data)
    /// </summary>
    public string ProducerId { get; set; } = string.Empty;

    /// <summary>
    /// Production interval in milliseconds
    /// </summary>
    public int ProductionIntervalMs { get; set; } = 100;

    /// <summary>
    /// Whether to use multicast
    /// </summary>
    public bool UseMulticast { get; set; } = false;

    /// <summary>
    /// Multicast group address (if using multicast)
    /// </summary>
    public string? MulticastGroup { get; set; }
}
