using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SuperPanel.DesktopApp.Views;

public class ViewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string currentView && parameter is string targetView)
        {
            return currentView == targetView ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}