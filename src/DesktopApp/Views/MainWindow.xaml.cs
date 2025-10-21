using SuperPanel.DesktopApp.ViewModels;
using System.Windows;

namespace SuperPanel.DesktopApp.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}