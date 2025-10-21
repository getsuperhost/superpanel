using System.Net.Http;
using System.Text;
using System.Text.Json;
using SuperPanel.DesktopApp.Models;
using Microsoft.Extensions.Configuration;

namespace SuperPanel.DesktopApp.Services;

public interface IApiService
{
    Task<List<Server>> GetServersAsync();
    Task<Server?> GetServerAsync(int id);
    Task<Server> CreateServerAsync(Server server);
    Task<Server?> UpdateServerAsync(int id, Server server);
    Task<bool> DeleteServerAsync(int id);
    Task<SystemInfo?> GetSystemInfoAsync();
    Task<List<Domain>> GetDomainsAsync();
    Task<List<FileSystemItem>> GetDirectoryContentsAsync(string path);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001";
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        
        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<List<Server>> GetServersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/servers");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<Server>>(json, _jsonOptions) ?? new List<Server>();
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error getting servers: {ex.Message}");
            return new List<Server>();
        }
    }

    public async Task<Server?> GetServerAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/servers/{id}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Server>(json, _jsonOptions);
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting server {id}: {ex.Message}");
            return null;
        }
    }

    public async Task<Server> CreateServerAsync(Server server)
    {
        try
        {
            var json = JsonSerializer.Serialize(server, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/servers", content);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Server>(responseJson, _jsonOptions) ?? server;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating server: {ex.Message}");
            return server;
        }
    }

    public async Task<Server?> UpdateServerAsync(int id, Server server)
    {
        try
        {
            var json = JsonSerializer.Serialize(server, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync($"/api/servers/{id}", content);
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Server>(responseJson, _jsonOptions);
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating server {id}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DeleteServerAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/servers/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting server {id}: {ex.Message}");
            return false;
        }
    }

    public async Task<SystemInfo?> GetSystemInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/servers/system-info");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<SystemInfo>(json, _jsonOptions);
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting system info: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Domain>> GetDomainsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/domains");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<Domain>>(json, _jsonOptions) ?? new List<Domain>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting domains: {ex.Message}");
            return new List<Domain>();
        }
    }

    public async Task<List<FileSystemItem>> GetDirectoryContentsAsync(string path)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/files/browse?path={Uri.EscapeDataString(path)}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<FileSystemItem>>(json, _jsonOptions) ?? new List<FileSystemItem>();
            }
            return new List<FileSystemItem>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting directory contents: {ex.Message}");
            return new List<FileSystemItem>();
        }
    }
}