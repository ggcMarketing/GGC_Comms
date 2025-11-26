using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using IndustrialGateway.Core.Models;

namespace IndustrialGateway.UI.Converters;

/// <summary>
/// Converts ConnectionState to a Brush color
/// </summary>
public class ConnectionStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionState state)
        {
            return state switch
            {
                ConnectionState.Connected => new SolidColorBrush(Colors.Green),
                ConnectionState.Connecting => new SolidColorBrush(Colors.Orange),
                ConnectionState.Disconnecting => new SolidColorBrush(Colors.Orange),
                ConnectionState.Error => new SolidColorBrush(Colors.Red),
                ConnectionState.Disconnected => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
