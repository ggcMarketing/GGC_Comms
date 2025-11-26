namespace IndustrialGateway.Core.Models;

/// <summary>
/// Represents the connection state of a protocol client
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Error
}
