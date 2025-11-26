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
    public void Constructor_ShouldPopulateSupportedProtocols()
    {
        // Assert
        _viewModel.SupportedProtocols.Should().NotBeEmpty();
        _viewModel.SupportedProtocols.Should().Contain(ProtocolType.ModbusTcp);
        _viewModel.SupportedProtocols.Should().Contain(ProtocolType.EtherNetIp);
        _viewModel.SupportedProtocols.Should().Contain(ProtocolType.Egd);
    }

    [Fact]
    public void SelectedProtocolType_WhenChanged_ShouldUpdateCurrentViewModel()
    {
        // Arrange
        var initialViewModel = _viewModel.CurrentProtocolViewModel;

        // Act
        _viewModel.SelectedProtocolType = ProtocolType.ModbusTcp;

        // Assert
        _viewModel.CurrentProtocolViewModel.Should().NotBeNull();
    }

    [Fact]
    public void SelectedProtocolType_ModbusTcp_ShouldCreateModbusTcpViewModel()
    {
        // Act
        _viewModel.SelectedProtocolType = ProtocolType.ModbusTcp;

        // Assert
        _viewModel.CurrentProtocolViewModel.Should().BeOfType<ModbusTcpConfigViewModel>();
    }
}
