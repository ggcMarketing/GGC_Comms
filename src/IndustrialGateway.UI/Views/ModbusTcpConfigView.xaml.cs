using System.Windows.Controls;
using IndustrialGateway.Core.Models;

namespace IndustrialGateway.UI.Views;

/// <summary>
/// Interaction logic for ModbusTcpConfigView.xaml
/// </summary>
public partial class ModbusTcpConfigView : UserControl
{
    public static Array DataTypeValues => Enum.GetValues(typeof(TagDataType));

    public ModbusTcpConfigView()
    {
        InitializeComponent();
    }
}
