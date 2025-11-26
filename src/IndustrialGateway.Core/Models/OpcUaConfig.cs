namespace IndustrialGateway.Core.Models;

/// <summary>
/// Configuration for OPC UA connections
/// </summary>
public class OpcUaConfig : ConnectionConfig
{
    public override ProtocolType ProtocolType => ProtocolType.OpcUa;

    /// <summary>
    /// OPC UA endpoint URL (e.g., "opc.tcp://localhost:4840")
    /// </summary>
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4840";

    /// <summary>
    /// Security policy (None, Basic128Rsa15, Basic256, Basic256Sha256)
    /// </summary>
    public string SecurityPolicy { get; set; } = "None";

    /// <summary>
    /// Message security mode (None, Sign, SignAndEncrypt)
    /// </summary>
    public string MessageSecurityMode { get; set; } = "None";

    /// <summary>
    /// Username for authentication (optional)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication (optional) - should be encrypted in production
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Application name
    /// </summary>
    public string ApplicationName { get; set; } = "IndustrialGateway";

    /// <summary>
    /// Session timeout in milliseconds
    /// </summary>
    public int SessionTimeoutMs { get; set; } = 60000;
}
