namespace IndustrialGateway.Core.Models;

/// <summary>
/// EGD operation mode
/// </summary>
public enum EgdMode
{
    /// <summary>
    /// Consumer mode - receives data from EGD producers
    /// </summary>
    Consumer,
    
    /// <summary>
    /// Producer mode - sends data to EGD consumers
    /// </summary>
    Producer,
    
    /// <summary>
    /// Both producer and consumer
    /// </summary>
    Both
}

/// <summary>
/// Configuration for GE EGD (Ethernet Global Data) connections
/// </summary>
public class EgdConfig : ConnectionConfig
{
    public override ProtocolType ProtocolType => ProtocolType.Egd;

    /// <summary>
    /// EGD operation mode (Producer, Consumer, or Both)
    /// </summary>
    public EgdMode Mode { get; set; } = EgdMode.Consumer;

    /// <summary>
    /// IP address or hostname of the EGD device (for Consumer mode)
    /// For Producer mode, this is the local interface to bind to
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
    /// In Consumer mode: ID of the producer to listen to
    /// In Producer mode: This gateway's producer ID
    /// </summary>
    public string ProducerId { get; set; } = string.Empty;

    /// <summary>
    /// Production interval in milliseconds (for Producer mode)
    /// How often to send data updates
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

    /// <summary>
    /// Consumer ID (for Consumer mode, optional)
    /// </summary>
    public string? ConsumerId { get; set; }
}
