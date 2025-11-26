using IndustrialGateway.Core.Models;
using System.Collections.ObjectModel;

namespace IndustrialGateway.UI.ViewModels;

/// <summary>
/// ViewModel representing a group of connections for a specific protocol
/// </summary>
public class ConnectionGroupViewModel : ViewModelBase
{
    private string _name;
    private bool _isExpanded;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ProtocolType ProtocolType { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<ConnectionViewModel> Connections { get; }

    public ConnectionGroupViewModel(ProtocolType protocolType, string name)
    {
        ProtocolType = protocolType;
        _name = name;
        _isExpanded = true;
        Connections = new ObservableCollection<ConnectionViewModel>();
    }
}
