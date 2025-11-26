namespace IndustrialGateway.Core.Models;

/// <summary>
/// Configuration for EtherNet/IP connections
/// </summary>
public class EtherNetIpConfig : ConnectionConfig
{
    public override ProtocolType ProtocolType => ProtocolType.EtherNetIp;

    /// <summary>
    /// IP address or hostname of the EtherNet/IP device
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// EtherNet/IP port (default 44818 for TCP, 2222 for UDP)
    /// </summary>
    public int Port { get; set; } = 44818;

    /// <summary>
    /// Processor slot number (for ControlLogix/CompactLogix)
    /// </summary>
    public byte ProcessorSlot { get; set; } = 0;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Use large forward open (for larger packet sizes)
    /// </summary>
    public bool UseLargeForwardOpen { get; set; } = false;

    /// <summary>
    /// Vendor ID (optional, for specific device compatibility)
    /// </summary>
    public ushort? VendorId { get; set; }
}
