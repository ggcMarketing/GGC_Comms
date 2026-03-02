using IndustrialGateway.Core.Interfaces;
using IndustrialGateway.Core.Models;

namespace IndustrialGateway.UI.ViewModels;

/// <summary>
/// ViewModel representing a single connection instance
/// </summary>
public class ConnectionViewModel : ViewModelBase
{
    private string _name;
    private ConnectionState _state;
    private ConnectionConfig _config;
    private object? _configViewModel;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ConnectionState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public ConnectionConfig Config
    {
        get => _config;
        set => SetProperty(ref _config, value);
    }

    public IProtocolClient? Client { get; set; }

    /// <summary>
    /// Protocol-specific configuration ViewModel (e.g., EgdConfigViewModel)
    /// </summary>
    public object? ConfigViewModel
    {
        get => _configViewModel;
        set => SetProperty(ref _configViewModel, value);
    }

    public ConnectionViewModel(ConnectionConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _name = config.Name;
        _state = ConnectionState.Disconnected;
    }
}
