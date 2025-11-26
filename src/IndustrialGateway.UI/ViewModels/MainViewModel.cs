using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace IndustrialGateway.UI.ViewModels;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IProtocolClientFactory _clientFactory;
    private readonly IConfigurationService _configService;
    private readonly ILoggerFactory _loggerFactory;

    private ProtocolType _selectedProtocolType = ProtocolType.ModbusTcp;
    private ProtocolConfigViewModel? _currentProtocolViewModel;

    public ObservableCollection<ProtocolType> SupportedProtocols { get; } = new();

    public ProtocolType SelectedProtocolType
    {
        get => _selectedProtocolType;
        set
        {
            if (SetProperty(ref _selectedProtocolType, value))
            {
                LoadProtocolConfiguration();
            }
        }
    }

    public ProtocolConfigViewModel? CurrentProtocolViewModel
    {
        get => _currentProtocolViewModel;
        set => SetProperty(ref _currentProtocolViewModel, value);
    }

    public MainViewModel(
        IProtocolClientFactory clientFactory,
        IConfigurationService configService,
        ILoggerFactory loggerFactory)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        // Populate supported protocols
        foreach (ProtocolType protocol in Enum.GetValues(typeof(ProtocolType)))
        {
            if (_clientFactory.SupportsProtocol(protocol))
            {
                SupportedProtocols.Add(protocol);
            }
        }

        // Load initial configuration
        LoadProtocolConfiguration();
    }

    private void LoadProtocolConfiguration()
    {
        // Dispose previous viewmodel
        _currentProtocolViewModel?.Dispose();

        // Create configuration and client for selected protocol
        ConnectionConfig config = SelectedProtocolType switch
        {
            ProtocolType.ModbusTcp => new ModbusTcpConfig { Name = "Modbus TCP Connection" },
            ProtocolType.OpcUa => new OpcUaConfig { Name = "OPC UA Connection" },
            ProtocolType.Mqtt => new MqttConfig { Name = "MQTT Connection" },
            ProtocolType.EtherNetIp => new EtherNetIpConfig { Name = "EtherNet/IP Connection" },
            ProtocolType.Egd => new EgdConfig { Name = "EGD Connection" },
            ProtocolType.Profinet => new ProfinetConfig { Name = "PROFINET Connection" },
            _ => new ModbusTcpConfig { Name = "Unknown" }
        };

        var client = _clientFactory.CreateClient(config);

        // Create appropriate viewmodel
        CurrentProtocolViewModel = config switch
        {
            ModbusTcpConfig modbusTcpConfig => new ModbusTcpConfigViewModel(
                modbusTcpConfig,
                client,
                _loggerFactory.CreateLogger<ModbusTcpConfigViewModel>()),
            // Add other protocol viewmodels here as they are implemented
            _ => null
        };
    }
}
