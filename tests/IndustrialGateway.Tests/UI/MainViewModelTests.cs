using FluentAssertions;
using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using IndustrialGateway.Protocols;
using IndustrialGateway.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IndustrialGateway.Tests.UI;

public class MainViewModelTests
{
    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly IProtocolClientFactory _clientFactory;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _configServiceMock = new Mock<IConfigurationService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();

        // Setup logger factory to return mock loggers
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        _clientFactory = new ProtocolClientFactory(_loggerFactoryMock.Object);

        _viewModel = new MainViewModel(
            _clientFactory,
            _configServiceMock.Object,
            _loggerFactoryMock.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeConnectionGroups()
    {
        // Assert
        _viewModel.ConnectionGroups.Should().NotBeEmpty();
        _viewModel.ConnectionGroups.Should().HaveCount(3);
        _viewModel.ConnectionGroups.Should().Contain(g => g.ProtocolType == ProtocolType.ModbusTcp);
        _viewModel.ConnectionGroups.Should().Contain(g => g.ProtocolType == ProtocolType.EtherNetIp);
        _viewModel.ConnectionGroups.Should().Contain(g => g.ProtocolType == ProtocolType.Egd);
    }

    [Fact]
    public void AddModbusTcpCommand_ShouldAddConnectionToGroup()
    {
        // Arrange
        var modbusTcpGroup = _viewModel.ConnectionGroups.First(g => g.ProtocolType == ProtocolType.ModbusTcp);
        var initialCount = modbusTcpGroup.Connections.Count;

        // Act
        _viewModel.AddModbusTcpCommand.Execute(null);

        // Assert
        modbusTcpGroup.Connections.Should().HaveCount(initialCount + 1);
        _viewModel.SelectedConnection.Should().NotBeNull();
    }

    [Fact]
    public void StatusMessage_ShouldInitializeToReady()
    {
        // Assert
        _viewModel.StatusMessage.Should().Be("Ready");
    }
}
