using IndustrialGateway.Core.Models;

namespace IndustrialGateway.Core.Interfaces;

/// <summary>
/// Service for managing connection configurations
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Load all configurations from storage
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of connection configurations</returns>
    Task<IEnumerable<ConnectionConfig>> LoadConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save a configuration to storage
    /// </summary>
    /// <param name="config">Configuration to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveConfigurationAsync(ConnectionConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a configuration from storage
    /// </summary>
    /// <param name="configId">Configuration ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DeleteConfigurationAsync(string configId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific configuration by ID
    /// </summary>
    /// <param name="configId">Configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration if found, null otherwise</returns>
    Task<ConnectionConfig?> GetConfigurationAsync(string configId, CancellationToken cancellationToken = default);
}
