namespace IndustrialGateway.Core.Models;

/// <summary>
/// Configuration for Modbus TCP connections
/// </summary>
public class ModbusTcpConfig : ConnectionConfig
{
    public override ProtocolType ProtocolType => ProtocolType.ModbusTcp;

    /// <summary>
    /// IP address or hostname of the Modbus server
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// TCP port (default 502)
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// Modbus slave/unit ID (default 1)
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Whether to use Modbus RTU over TCP (default is Modbus TCP)
    /// </summary>
    public bool UseRtuOverTcp { get; set; } = false;
}
