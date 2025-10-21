using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SuperPanel.DesktopApp.Models;

namespace SuperPanel.DesktopApp.Views;

public partial class ServersView : UserControl
{
    public ServersView()
    {
        InitializeComponent();
    }
}

public class ServerStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ServerStatus status)
        {
            return status switch
            {
                ServerStatus.Online => Colors.Green,
                ServerStatus.Offline => Colors.Red,
                ServerStatus.Maintenance => Colors.Orange,
                ServerStatus.Error => Colors.DarkRed,
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}