namespace IndustrialGateway.Core.Models;

/// <summary>
/// Configuration for Siemens S7 PLC connections
/// </summary>
public class S7Config : ConnectionConfig
{
    public override ProtocolType ProtocolType => ProtocolType.S7;

    /// <summary>
    /// Host IP address or hostname
    /// </summary>
    public string Host { get; set; } = "192.168.1.1";

    /// <summary>
    /// Port number (default 102 for S7)
    /// </summary>
    public int Port { get; set; } = 102;

    /// <summary>
    /// CPU Type (S7-200, S7-300, S7-400, S7-1200, S7-1500)
    /// </summary>
    public S7CpuType CpuType { get; set; } = S7CpuType.S71200;

    /// <summary>
    /// Rack number (typically 0)
    /// </summary>
    public short Rack { get; set; } = 0;

    /// <summary>
    /// Slot number (typically 1 or 2)
    /// </summary>
    public short Slot { get; set; } = 1;
}

/// <summary>
/// Siemens S7 CPU Types
/// </summary>
public enum S7CpuType
{
    S7200 = 0,
    S7300 = 10,
    S7400 = 20,
    S71200 = 30,
    S71500 = 40
}
