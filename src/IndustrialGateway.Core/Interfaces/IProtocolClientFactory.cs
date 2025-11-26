using IndustrialGateway.Core.Models;

namespace IndustrialGateway.Core.Interfaces;

/// <summary>
/// Factory for creating protocol clients
/// </summary>
public interface IProtocolClientFactory
{
    /// <summary>
    /// Create a protocol client for the given configuration
    /// </summary>
    /// <param name="config">Connection configuration</param>
    /// <returns>Protocol client instance</returns>
    IProtocolClient CreateClient(ConnectionConfig config);

    /// <summary>
    /// Check if the factory supports the given protocol type
    /// </summary>
    /// <param name="protocolType">Protocol type</param>
    /// <returns>True if supported, false otherwise</returns>
    bool SupportsProtocol(ProtocolType protocolType);
}
