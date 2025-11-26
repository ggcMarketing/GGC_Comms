using System.Windows;
using System.Windows.Controls;
using IndustrialGateway.UI.ViewModels;

namespace IndustrialGateway.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ConnectionTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is ConnectionViewModel connection)
        {
            viewModel.SelectedConnection = connection;
        }
    }
}
