using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;
using IndustrialGateway.UI.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace IndustrialGateway.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IProtocolClientFactory _clientFactory;
    private readonly IConfigurationService _configService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MainViewModel> _logger;

    private ConnectionViewModel? _selectedConnection;
    private string _statusMessage = "Ready";
    private int _activeConnectionCount;
    private string? _currentConfigurationFile;

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
    public ICommand AddConnectionToGroupCommand { get; }
    public ICommand RemoveConnectionCommand { get; }
    public ICommand NewConfigurationCommand { get; }
    public ICommand OpenConfigurationCommand { get; }
    public ICommand SaveConfigurationCommand { get; }
    public ICommand SaveAsConfigurationCommand { get; }
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
        _logger = loggerFactory.CreateLogger<MainViewModel>();

        ConnectionGroups = new ObservableCollection<ConnectionGroupViewModel>();

        InitializeConnectionGroups();

        AddModbusTcpCommand = new RelayCommand(_ => AddConnection(ProtocolType.ModbusTcp));
        AddEtherNetIpCommand = new RelayCommand(_ => AddConnection(ProtocolType.EtherNetIp));
        AddEgdCommand = new RelayCommand(_ => AddConnection(ProtocolType.Egd));
        AddConnectionToGroupCommand = new RelayCommand(param => AddConnectionToGroup(param));
        RemoveConnectionCommand = new RelayCommand(param => RemoveConnection(param));
        NewConfigurationCommand = new RelayCommand(_ => NewConfiguration());
        OpenConfigurationCommand = new AsyncRelayCommand(async _ => await OpenConfigurationAsync());
        SaveConfigurationCommand = new AsyncRelayCommand(async _ => await SaveConfigurationAsync());
        SaveAsConfigurationCommand = new AsyncRelayCommand(async _ => await SaveAsConfigurationAsync());
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
                Port = 18246,
                ExchangeId = 1,
                ProducerId = "GGC_Gateway"
            },
            _ => throw new NotSupportedException($"Protocol {protocolType} not supported")
        };

        var connectionViewModel = new ConnectionViewModel(config);
        
        // Create protocol client
        var client = _clientFactory.CreateClient(config);
        connectionViewModel.Client = client;

        // Create protocol-specific ViewModel
        connectionViewModel.ConfigViewModel = config switch
        {
            EgdConfig egdConfig => new EgdConfigViewModel(
                egdConfig,
                client,
                _loggerFactory.CreateLogger<EgdConfigViewModel>()),
            // Add other protocol ViewModels here as they are implemented
            _ => null
        };

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

    private void NewConfiguration()
    {
        var result = System.Windows.MessageBox.Show(
            "Create a new configuration? This will clear all current connections.",
            "New Configuration",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            foreach (var group in ConnectionGroups)
            {
                group.Connections.Clear();
            }

            _currentConfigurationFile = null;
            StatusMessage = "New configuration created";
            _logger.LogInformation("Created new configuration");
        }
    }

    private async Task OpenConfigurationAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Configuration",
                Filter = "JSON Configuration Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                InitialDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IndustrialGateway",
                    "Configurations")
            };

            if (dialog.ShowDialog() == true)
            {
                StatusMessage = "Loading configuration...";
                _currentConfigurationFile = dialog.FileName;

                // Load from the selected file
                var json = await File.ReadAllTextAsync(_currentConfigurationFile);
                var configs = System.Text.Json.JsonSerializer.Deserialize<List<ConnectionConfig>>(json,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (configs != null)
                {
                    // Clear existing connections
                    foreach (var group in ConnectionGroups)
                    {
                        group.Connections.Clear();
                    }

                    // Add loaded connections
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

                                var client = _clientFactory.CreateClient(config);
                                connectionViewModel.Client = client;

                                connectionViewModel.ConfigViewModel = config switch
                                {
                                    EgdConfig egdConfig => new EgdConfigViewModel(
                                        egdConfig,
                                        client,
                                        _loggerFactory.CreateLogger<EgdConfigViewModel>()),
                                    _ => null
                                };

                                group.Connections.Add(connectionViewModel);
                            }
                        }
                    }

                    StatusMessage = $"Loaded configuration from {Path.GetFileName(_currentConfigurationFile)}";
                    _logger.LogInformation("Loaded configuration from {File}", _currentConfigurationFile);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening configuration: {ex.Message}";
            _logger.LogError(ex, "Failed to open configuration");
            System.Windows.MessageBox.Show(
                $"Failed to open configuration:\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task SaveAsConfigurationAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Configuration As",
                Filter = "JSON Configuration Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = "gateway-config.json",
                InitialDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IndustrialGateway",
                    "Configurations")
            };

            if (dialog.ShowDialog() == true)
            {
                _currentConfigurationFile = dialog.FileName;
                await SaveToFileAsync(_currentConfigurationFile);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving configuration: {ex.Message}";
            _logger.LogError(ex, "Failed to save configuration");
            System.Windows.MessageBox.Show(
                $"Failed to save configuration:\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_currentConfigurationFile))
            {
                // No file selected, use Save As
                await SaveAsConfigurationAsync();
            }
            else
            {
                // Save to current file
                await SaveToFileAsync(_currentConfigurationFile);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving configuration: {ex.Message}";
            _logger.LogError(ex, "Failed to save configuration");
        }
    }

    private async Task SaveToFileAsync(string filePath)
    {
        StatusMessage = "Saving configuration...";

        var allConfigs = ConnectionGroups
            .SelectMany(g => g.Connections)
            .Select(c => c.Config)
            .ToList();

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Serialize to JSON
        var json = System.Text.Json.JsonSerializer.Serialize(allConfigs,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });

        await File.WriteAllTextAsync(filePath, json);

        StatusMessage = $"Configuration saved to {Path.GetFileName(filePath)} ({allConfigs.Count} connections)";
        _logger.LogInformation("Saved configuration to {File}", filePath);
    }



    private void ExitApplication()
    {
        System.Windows.Application.Current?.Shutdown();
    }

    private void AddConnectionToGroup(object? parameter)
    {
        _logger.LogInformation("AddConnectionToGroup called with parameter: {Parameter}", parameter?.GetType().Name);
        
        if (parameter is ConnectionGroupViewModel group)
        {
            _logger.LogInformation("Adding connection to group: {GroupName}", group.Name);
            AddConnection(group.ProtocolType);
        }
        else
        {
            _logger.LogWarning("Parameter is not ConnectionGroupViewModel: {Type}", parameter?.GetType().Name ?? "null");
        }
    }

    private void RemoveConnection(object? parameter)
    {
        _logger.LogInformation("RemoveConnection called with parameter: {Parameter}", parameter?.GetType().Name);
        
        if (parameter is ConnectionViewModel connection)
        {
            _logger.LogInformation("Removing connection: {ConnectionName}", connection.Name);
            
            // Find the group containing this connection
            var group = ConnectionGroups.FirstOrDefault(g => g.Connections.Contains(connection));
            if (group != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Remove connection '{connection.Name}'?",
                    "Remove Connection",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Disconnect if connected
                    if (connection.State == ConnectionState.Connected)
                    {
                        connection.Client?.DisconnectAsync();
                    }

                    group.Connections.Remove(connection);
                    
                    if (SelectedConnection == connection)
                    {
                        SelectedConnection = null;
                    }

                    StatusMessage = $"Removed connection: {connection.Name}";
                    _logger.LogInformation("Removed connection: {ConnectionName}", connection.Name);
                }
                else
                {
                    _logger.LogInformation("User cancelled removal");
                }
            }
            else
            {
                _logger.LogWarning("Could not find group containing connection");
            }
        }
        else
        {
            _logger.LogWarning("Parameter is not ConnectionViewModel: {Type}", parameter?.GetType().Name ?? "null");
        }
    }

    private void ShowAboutDialog()
    {
        System.Windows.MessageBox.Show(
            "Industrial Gateway v1.0\n\nGGC Automation\n\nIndustrial protocol communication gateway supporting:\n• Modbus TCP\n• EtherNet/IP\n• GE EGD",
            "About Industrial Gateway",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}
