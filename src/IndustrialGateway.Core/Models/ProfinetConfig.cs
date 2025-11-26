namespace IndustrialGateway.Core.Models;

/// <summary>
/// Configuration for PROFINET connections
/// </summary>
public class ProfinetConfig : ConnectionConfig
{
    public override ProtocolType ProtocolType => ProtocolType.Profinet;

    /// <summary>
    /// IP address or hostname of the PROFINET device
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Device name (PROFINET station name)
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Vendor ID
    /// </summary>
    public ushort VendorId { get; set; } = 0;

    /// <summary>
    /// Device ID
    /// </summary>
    public ushort DeviceId { get; set; } = 0;

    /// <summary>
    /// Rack number
    /// </summary>
    public int Rack { get; set; } = 0;

    /// <summary>
    /// Slot number
    /// </summary>
    public int Slot { get; set; } = 0;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Use PN-RT (Real-Time) communication
    /// </summary>
    public bool UseRealTime { get; set; } = false;
}
