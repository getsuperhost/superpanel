using SuperPanel.DesktopApp.Services;
using SuperPanel.DesktopApp.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SuperPanel.DesktopApp.ViewModels;

public class ServerViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private Server? _selectedServer;
    private bool _isLoading;

    public ServerViewModel(IApiService apiService)
    {
        _apiService = apiService;
        Servers = new ObservableCollection<Server>();
        
        // Initialize commands
        LoadServersCommand = new RelayCommand(async () => await LoadServersAsync());
        AddServerCommand = new RelayCommand(async () => await AddServerAsync());
        EditServerCommand = new RelayCommand<Server>(async (server) => await EditServerAsync(server));
        DeleteServerCommand = new RelayCommand<Server>(async (server) => await DeleteServerAsync(server));
        RefreshServerCommand = new RelayCommand<Server>(async (server) => await RefreshServerAsync(server));
        
        // Load initial data
        _ = LoadServersAsync();
    }

    public ObservableCollection<Server> Servers { get; }

    public Server? SelectedServer
    {
        get => _selectedServer;
        set => SetProperty(ref _selectedServer, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand LoadServersCommand { get; }
    public ICommand AddServerCommand { get; }
    public ICommand EditServerCommand { get; }
    public ICommand DeleteServerCommand { get; }
    public ICommand RefreshServerCommand { get; }

    private async Task LoadServersAsync()
    {
        IsLoading = true;
        try
        {
            var servers = await _apiService.GetServersAsync();
            Servers.Clear();
            foreach (var server in servers)
            {
                Servers.Add(server);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading servers: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddServerAsync()
    {
        // This would typically open a dialog or navigate to an add server view
        var newServer = new Server
        {
            Name = $"Server {Servers.Count + 1}",
            IpAddress = "192.168.1.100",
            Description = "New server",
            OperatingSystem = "Windows Server 2022",
            Status = ServerStatus.Offline
        };

        try
        {
            var createdServer = await _apiService.CreateServerAsync(newServer);
            Servers.Add(createdServer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding server: {ex.Message}");
        }
    }

    private async Task EditServerAsync(Server? server)
    {
        if (server == null) return;

        try
        {
            var updatedServer = await _apiService.UpdateServerAsync(server.Id, server);
            if (updatedServer != null)
            {
                var index = Servers.ToList().FindIndex(s => s.Id == server.Id);
                if (index >= 0)
                {
                    Servers[index] = updatedServer;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating server: {ex.Message}");
        }
    }

    private async Task DeleteServerAsync(Server? server)
    {
        if (server == null) return;

        try
        {
            var success = await _apiService.DeleteServerAsync(server.Id);
            if (success)
            {
                Servers.Remove(server);
                if (SelectedServer?.Id == server.Id)
                {
                    SelectedServer = null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting server: {ex.Message}");
        }
    }

    private async Task RefreshServerAsync(Server? server)
    {
        if (server == null) return;

        try
        {
            var refreshedServer = await _apiService.GetServerAsync(server.Id);
            if (refreshedServer != null)
            {
                var index = Servers.ToList().FindIndex(s => s.Id == server.Id);
                if (index >= 0)
                {
                    Servers[index] = refreshedServer;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing server: {ex.Message}");
        }
    }
}