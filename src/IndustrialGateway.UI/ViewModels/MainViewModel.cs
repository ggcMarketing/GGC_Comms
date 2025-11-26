using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using IndustrialGateway.UI.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace IndustrialGateway.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IProtocolClientFactory _clientFactory;
    private readonly IConfigurationService _configService;
    private readonly ILoggerFactory _loggerFactory;

    private ConnectionViewModel? _selectedConnection;
    private string _statusMessage = "Ready";
    private int _activeConnectionCount;

    public ObservableCollection<ConnectionGroupViewModel> ConnectionGroups { get; }

    public ConnectionViewModel? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (SetProperty(ref _selectedConnection, value))
            {
                LoadConnectionConfiguration();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int ActiveConnectionCount
    {
        get => _activeConnectionCount;
        set => SetProperty(ref _activeConnectionCount, value);
    }

    public ICommand AddModbusTcpCommand { get; }
    public ICommand AddEtherNetIpCommand { get; }
    public ICommand AddEgdCommand { get; }
    public ICommand NewConnectionCommand { get; }
    public ICommand SaveConfigurationCommand { get; }
    public ICommand LoadConfigurationCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AboutCommand { get; }

    public MainViewModel(
        IProtocolClientFactory clientFactory,
        IConfigurationService configService,
        ILoggerFactory loggerFactory)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        ConnectionGroups = new ObservableCollection<ConnectionGroupViewModel>();

        InitializeConnectionGroups();

        AddModbusTcpCommand = new RelayCommand(_ => AddConnection(ProtocolType.ModbusTcp));
        AddEtherNetIpCommand = new RelayCommand(_ => AddConnection(ProtocolType.EtherNetIp));
        AddEgdCommand = new RelayCommand(_ => AddConnection(ProtocolType.Egd));
        NewConnectionCommand = new RelayCommand(_ => ShowNewConnectionDialog());
        SaveConfigurationCommand = new AsyncRelayCommand(async _ => await SaveConfigurationAsync());
        LoadConfigurationCommand = new AsyncRelayCommand(async _ => await LoadConfigurationAsync());
        ExitCommand = new RelayCommand(_ => ExitApplication());
        AboutCommand = new RelayCommand(_ => ShowAboutDialog());

        StatusMessage = "Ready";
    }

    private void InitializeConnectionGroups()
    {
        ConnectionGroups.Add(new ConnectionGroupViewModel(ProtocolType.ModbusTcp, "Modbus TCP"));
        ConnectionGroups.Add(new ConnectionGroupViewModel(ProtocolType.EtherNetIp, "EtherNet/IP"));
        ConnectionGroups.Add(new ConnectionGroupViewModel(ProtocolType.Egd, "GE EGD"));
    }

    private void AddConnection(ProtocolType protocolType)
    {
        var group = ConnectionGroups.FirstOrDefault(g => g.ProtocolType == protocolType);
        if (group == null) return;

        ConnectionConfig config = protocolType switch
        {
            ProtocolType.ModbusTcp => new ModbusTcpConfig 
            { 
                Name = $"Modbus TCP {group.Connections.Count + 1}",
                Host = "127.0.0.1",
                Port = 502
            },
            ProtocolType.EtherNetIp => new EtherNetIpConfig 
            { 
                Name = $"EtherNet/IP {group.Connections.Count + 1}",
                Host = "192.168.1.1",
                Port = 44818
            },
            ProtocolType.Egd => new EgdConfig 
            { 
                Name = $"GE EGD {group.Connections.Count + 1}",
                Host = "192.168.1.1",
                Port = 18246
            },
            _ => throw new NotSupportedException($"Protocol {protocolType} not supported")
        };

        var connectionViewModel = new ConnectionViewModel(config);
        group.Connections.Add(connectionViewModel);
        group.IsExpanded = true;
        SelectedConnection = connectionViewModel;

        StatusMessage = $"Added new {protocolType} connection";
    }

    private void LoadConnectionConfiguration()
    {
        if (SelectedConnection == null)
        {
            StatusMessage = "No connection selected";
            return;
        }

        StatusMessage = $"Selected: {SelectedConnection.Name}";
    }

    private void ShowNewConnectionDialog()
    {
        StatusMessage = "New connection dialog - not implemented";
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            StatusMessage = "Saving configuration...";
            
            var allConfigs = ConnectionGroups
                .SelectMany(g => g.Connections)
                .Select(c => c.Config)
                .ToList();

            foreach (var config in allConfigs)
            {
                await _configService.SaveConfigurationAsync(config);
            }
            
            StatusMessage = $"Configuration saved successfully ({allConfigs.Count} connections)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving configuration: {ex.Message}";
        }
    }

    private async Task LoadConfigurationAsync()
    {
        try
        {
            StatusMessage = "Loading configuration...";
            
            var configs = await _configService.LoadConfigurationsAsync();
            
            foreach (var group in ConnectionGroups)
            {
                group.Connections.Clear();
            }

            foreach (var config in configs)
            {
                var protocolType = config switch
                {
                    ModbusTcpConfig => ProtocolType.ModbusTcp,
                    EtherNetIpConfig => ProtocolType.EtherNetIp,
                    EgdConfig => ProtocolType.Egd,
                    _ => (ProtocolType?)null
                };

                if (protocolType.HasValue)
                {
                    var group = ConnectionGroups.FirstOrDefault(g => g.ProtocolType == protocolType.Value);
                    if (group != null)
                    {
                        var connectionViewModel = new ConnectionViewModel(config);
                        group.Connections.Add(connectionViewModel);
                    }
                }
            }

            StatusMessage = $"Configuration loaded successfully ({configs.Count()} connections)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading configuration: {ex.Message}";
        }
    }

    private void ExitApplication()
    {
        System.Windows.Application.Current?.Shutdown();
    }

    private void ShowAboutDialog()
    {
        StatusMessage = "Industrial Gateway v1.0 - GGC Automation";
    }
}
