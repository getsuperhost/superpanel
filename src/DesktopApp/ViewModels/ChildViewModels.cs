using SuperPanel.DesktopApp.Services;
using SuperPanel.DesktopApp.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SuperPanel.DesktopApp.ViewModels;

public class DomainViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private Domain? _selectedDomain;
    private bool _isLoading;

    public DomainViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Domains = new ObservableCollection<Domain>();
        
        LoadDomainsCommand = new RelayCommand(async () => await LoadDomainsAsync());
        
        _ = LoadDomainsAsync();
    }

    public ObservableCollection<Domain> Domains { get; }

    public Domain? SelectedDomain
    {
        get => _selectedDomain;
        set => SetProperty(ref _selectedDomain, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand LoadDomainsCommand { get; }

    private async Task LoadDomainsAsync()
    {
        IsLoading = true;
        try
        {
            var domains = await _apiService.GetDomainsAsync();
            Domains.Clear();
            foreach (var domain in domains)
            {
                Domains.Add(domain);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading domains: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class FileManagerViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private string _currentPath = "/";
    private bool _isLoading;

    public FileManagerViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Files = new ObservableCollection<FileSystemItem>();
        
        LoadDirectoryCommand = new RelayCommand(async () => await LoadDirectoryAsync());
        NavigateCommand = new RelayCommand<string>(async (path) => await NavigateAsync(path));
        
        _ = LoadDirectoryAsync();
    }

    public ObservableCollection<FileSystemItem> Files { get; }

    public string CurrentPath
    {
        get => _currentPath;
        set
        {
            if (SetProperty(ref _currentPath, value))
            {
                _ = LoadDirectoryAsync();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand LoadDirectoryCommand { get; }
    public ICommand NavigateCommand { get; }

    private async Task LoadDirectoryAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _apiService.GetDirectoryContentsAsync(CurrentPath);
            Files.Clear();
            foreach (var item in items)
            {
                Files.Add(item);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading directory: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task NavigateAsync(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            CurrentPath = path;
        }
    }
}

public class MonitoringViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly ISystemService _systemService;
    private SystemInfo? _systemInfo;
    private bool _isLoading;
    private System.Timers.Timer? _refreshTimer;

    public MonitoringViewModel(IApiService apiService, ISystemService systemService)
    {
        _apiService = apiService;
        _systemService = systemService;
        
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        
        // Start auto-refresh timer
        _refreshTimer = new System.Timers.Timer(5000); // 5 seconds
        _refreshTimer.Elapsed += async (s, e) => await RefreshAsync();
        _refreshTimer.Start();
        
        _ = RefreshAsync();
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

    public ICommand RefreshCommand { get; }

    private async Task RefreshAsync()
    {
        if (IsLoading) return; // Prevent multiple simultaneous refreshes

        IsLoading = true;
        try
        {
            // Try API first, then local
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
            Console.WriteLine($"Error refreshing monitoring data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }
}