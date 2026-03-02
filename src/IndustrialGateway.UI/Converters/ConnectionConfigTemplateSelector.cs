using IndustrialGateway.Core.Models;
using IndustrialGateway.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace IndustrialGateway.UI.Converters;

public class ConnectionConfigTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ModbusTcpTemplate { get; set; }
    public DataTemplate? EtherNetIpTemplate { get; set; }
    public DataTemplate? EgdTemplate { get; set; }
    public DataTemplate? S7Template { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is ConnectionViewModel connectionViewModel)
        {
            return connectionViewModel.Config switch
            {
                ModbusTcpConfig => ModbusTcpTemplate,
                EtherNetIpConfig => EtherNetIpTemplate,
                EgdConfig => EgdTemplate,
                S7Config => S7Template,
                _ => DefaultTemplate
            };
        }

        return DefaultTemplate;
    }
}
