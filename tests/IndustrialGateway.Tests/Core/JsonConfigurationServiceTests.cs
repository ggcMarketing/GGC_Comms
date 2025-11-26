using FluentAssertions;
using IndustrialGateway.Core.Models;
using IndustrialGateway.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IndustrialGateway.Tests.Core;

public class JsonConfigurationServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly JsonConfigurationService _service;
    private readonly Mock<ILogger<JsonConfigurationService>> _loggerMock;

    public JsonConfigurationServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"IndustrialGatewayTests_{Guid.NewGuid()}");
        _loggerMock = new Mock<ILogger<JsonConfigurationService>>();
        _service = new JsonConfigurationService(_loggerMock.Object, _testDirectory);
    }

    [Fact]
    public async Task SaveConfigurationAsync_ShouldSaveConfigToFile()
    {
        // Arrange
        var config = new ModbusTcpConfig
        {
            Name = "Test Modbus",
            Host = "192.168.1.100",
            Port = 502
        };

        // Act
        await _service.SaveConfigurationAsync(config);

        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.json");
        files.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadConfigurationsAsync_ShouldLoadSavedConfigurations()
    {
        // Arrange
        var config1 = new ModbusTcpConfig
        {
            Name = "Modbus 1",
            Host = "192.168.1.100",
            Port = 502
        };

        var config2 = new EtherNetIpConfig
        {
            Name = "EtherNet/IP 1",
            Host = "192.168.1.50",
            Port = 44818
        };

        await _service.SaveConfigurationAsync(config1);
        await _service.SaveConfigurationAsync(config2);

        // Act
        var loaded = (await _service.LoadConfigurationsAsync()).ToList();

        // Assert
        loaded.Should().HaveCount(2);
        loaded.Should().ContainSingle(c => c.Name == "Modbus 1");
        loaded.Should().ContainSingle(c => c.Name == "EtherNet/IP 1");
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldRemoveConfiguration()
    {
        // Arrange
        var config = new ModbusTcpConfig
        {
            Name = "Test Delete",
            Host = "192.168.1.100"
        };

        await _service.SaveConfigurationAsync(config);
        var configs = await _service.LoadConfigurationsAsync();
        configs.Should().NotBeEmpty();

        // Act
        await _service.DeleteConfigurationAsync(config.Id);

        // Assert
        var remaining = await _service.LoadConfigurationsAsync();
        remaining.Should().NotContain(c => c.Id == config.Id);
    }

    [Fact]
    public async Task GetConfigurationAsync_ShouldReturnSpecificConfiguration()
    {
        // Arrange
        var config = new ModbusTcpConfig
        {
            Name = "Test Get",
            Host = "192.168.1.100"
        };

        await _service.SaveConfigurationAsync(config);

        // Act
        var retrieved = await _service.GetConfigurationAsync(config.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Get");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
