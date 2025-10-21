using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace SuperPanel.DesktopApp.Views;

public partial class FileManagerView : UserControl
{
    public FileManagerView()
    {
        InitializeComponent();
    }
}

public class FileTypeIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDirectory)
        {
            return isDirectory ? "📁" : "📄";
        }
        return "📄";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024:F1} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024):F1} MB";
            else
                return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}