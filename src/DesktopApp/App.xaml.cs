using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SuperPanel.DesktopApp.Services;
using SuperPanel.DesktopApp.ViewModels;
using SuperPanel.DesktopApp.Views;
using System.Windows;
using ModernWpf;

namespace SuperPanel.DesktopApp;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        var builder = Host.CreateDefaultBuilder(e.Args);
        
        builder.ConfigureServices((context, services) =>
        {
            // Register services
            services.AddSingleton<IApiService, ApiService>();
            services.AddSingleton<ISystemService, SystemService>();
            
            // Register ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<ServerViewModel>();
            services.AddTransient<DomainViewModel>();
            services.AddTransient<FileManagerViewModel>();
            services.AddTransient<MonitoringViewModel>();
            
            // Register Views
            services.AddTransient<MainWindow>();
        });

        _host = builder.Build();

        // Apply Modern WPF theme
        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
        ThemeManager.Current.AccentColor = System.Windows.Media.Colors.DeepSkyBlue;

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}