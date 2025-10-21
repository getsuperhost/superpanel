using SuperPanel.DesktopApp.Services;
using SuperPanel.DesktopApp.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows;

namespace SuperPanel.DesktopApp.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly ISystemService _systemService;
    
    private string _selectedView = "Dashboard";
    private SystemInfo? _systemInfo;
    private bool _isLoading;

    public MainViewModel(IApiService apiService, ISystemService systemService)
    {
        _apiService = apiService;
        _systemService = systemService;
        
        // Initialize commands
        NavigateCommand = new RelayCommand<string>(Navigate);
        RefreshSystemInfoCommand = new RelayCommand(async () => await RefreshSystemInfoAsync());
        
        // Initialize child view models
        ServerViewModel = new ServerViewModel(_apiService);
        DomainViewModel = new DomainViewModel(_apiService);
        FileManagerViewModel = new FileManagerViewModel(_apiService);
        MonitoringViewModel = new MonitoringViewModel(_apiService, _systemService);
        
        // Load initial data
        _ = LoadInitialDataAsync();
    }

    public string SelectedView
    {
        get => _selectedView;
        set => SetProperty(ref _selectedView, value);
    }

    public SystemInfo? SystemInfo
    {
        get => _systemInfo;
        set => SetProperty(ref _systemInfo, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand NavigateCommand { get; }
    public ICommand RefreshSystemInfoCommand { get; }

    // Child ViewModels
    public ServerViewModel ServerViewModel { get; }
    public DomainViewModel DomainViewModel { get; }
    public FileManagerViewModel FileManagerViewModel { get; }
    public MonitoringViewModel MonitoringViewModel { get; }

    private void Navigate(string? viewName)
    {
        if (!string.IsNullOrEmpty(viewName))
        {
            SelectedView = viewName;
        }
    }

    private async Task LoadInitialDataAsync()
    {
        IsLoading = true;
        try
        {
            await RefreshSystemInfoAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading initial data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshSystemInfoAsync()
    {
        try
        {
            // Try to get system info from API first, then fall back to local
            var apiSystemInfo = await _apiService.GetSystemInfoAsync();
            if (apiSystemInfo != null)
            {
                SystemInfo = apiSystemInfo;
            }
            else
            {
                SystemInfo = await _systemService.GetLocalSystemInfoAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing system info: {ex.Message}");
            // Fallback to local system info
            try
            {
                SystemInfo = await _systemService.GetLocalSystemInfoAsync();
            }
            catch (Exception localEx)
            {
                Console.WriteLine($"Error getting local system info: {localEx.Message}");
            }
        }
    }
}

// Simple RelayCommand implementation
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
}