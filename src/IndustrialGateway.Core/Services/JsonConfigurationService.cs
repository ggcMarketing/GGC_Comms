using System.Text.Json;
using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace IndustrialGateway.Core.Services;

/// <summary>
/// Configuration service that persists to JSON files
/// </summary>
public class JsonConfigurationService : IConfigurationService
{
    private readonly string _configDirectory;
    private readonly ILogger<JsonConfigurationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonConfigurationService(
        ILogger<JsonConfigurationService> logger,
        string? configDirectory = null)
    {
        _logger = logger;
        _configDirectory = configDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IndustrialGateway",
            "Configurations");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Ensure directory exists
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
            _logger.LogInformation("Created configuration directory: {Directory}", _configDirectory);
        }
    }

    public async Task<IEnumerable<ConnectionConfig>> LoadConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var configs = new List<ConnectionConfig>();
            var files = Directory.GetFiles(_configDirectory, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var config = JsonSerializer.Deserialize<ConnectionConfig>(json, _jsonOptions);

                    if (config != null)
                    {
                        configs.Add(config);
                        _logger.LogInformation("Loaded configuration: {Name} ({Type})", config.Name, config.ProtocolType);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load configuration from file: {File}", file);
                }
            }

            return configs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configurations from directory: {Directory}", _configDirectory);
            return Array.Empty<ConnectionConfig>();
        }
    }

    public async Task SaveConfigurationAsync(ConnectionConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = $"{SanitizeFileName(config.Name)}_{config.Id}.json";
            var filePath = Path.Combine(_configDirectory, fileName);

            var json = JsonSerializer.Serialize(config, config.GetType(), _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation("Saved configuration: {Name} to {File}", config.Name, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration: {Name}", config.Name);
            throw;
        }
    }

    public async Task DeleteConfigurationAsync(string configId, CancellationToken cancellationToken = default)
    {
        try
        {
            var files = Directory.GetFiles(_configDirectory, $"*_{configId}.json");

            foreach (var file in files)
            {
                File.Delete(file);
                _logger.LogInformation("Deleted configuration file: {File}", file);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete configuration: {ConfigId}", configId);
            throw;
        }
    }

    public async Task<ConnectionConfig?> GetConfigurationAsync(string configId, CancellationToken cancellationToken = default)
    {
        try
        {
            var configs = await LoadConfigurationsAsync(cancellationToken);
            return configs.FirstOrDefault(c => c.Id == configId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration: {ConfigId}", configId);
            return null;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
