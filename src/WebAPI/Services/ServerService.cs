using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public interface IServerService
{
    Task<List<Server>> GetAllServersAsync();
    Task<Server?> GetServerByIdAsync(int id);
    Task<Server> CreateServerAsync(Server server);
    Task<Server?> UpdateServerAsync(int id, Server server);
    Task<bool> DeleteServerAsync(int id);
    Task<bool> UpdateServerStatusAsync(int id, ServerStatus status);
}

public class ServerService : IServerService
{
    private readonly ApplicationDbContext _context;
    private readonly ISystemMonitoringService _systemMonitoring;

    public ServerService(ApplicationDbContext context, ISystemMonitoringService systemMonitoring)
    {
        _context = context;
        _systemMonitoring = systemMonitoring;
    }

    public async Task<List<Server>> GetAllServersAsync()
    {
        return await _context.Servers
            .Include(s => s.Domains)
            .Include(s => s.Databases)
            .ToListAsync();
    }

    public async Task<Server?> GetServerByIdAsync(int id)
    {
        return await _context.Servers
            .Include(s => s.Domains)
            .Include(s => s.Databases)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Server> CreateServerAsync(Server server)
    {
        server.CreatedAt = DateTime.UtcNow;
        _context.Servers.Add(server);
        await _context.SaveChangesAsync();
        return server;
    }

    public async Task<Server?> UpdateServerAsync(int id, Server server)
    {
        var existingServer = await _context.Servers.FindAsync(id);
        if (existingServer == null)
            return null;

        existingServer.Name = server.Name;
        existingServer.IpAddress = server.IpAddress;
        existingServer.Description = server.Description;
        existingServer.OperatingSystem = server.OperatingSystem;
        existingServer.Status = server.Status;

        await _context.SaveChangesAsync();
        return existingServer;
    }

    public async Task<bool> DeleteServerAsync(int id)
    {
        var server = await _context.Servers.FindAsync(id);
        if (server == null)
            return false;

        _context.Servers.Remove(server);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateServerStatusAsync(int id, ServerStatus status)
    {
        var server = await _context.Servers.FindAsync(id);
        if (server == null)
            return false;

        server.Status = status;
        server.LastChecked = DateTime.UtcNow;

        // Update system metrics if server is online
        if (status == ServerStatus.Online)
        {
            try
            {
                server.CpuUsage = await _systemMonitoring.GetCpuUsageAsync();
                server.MemoryUsage = (double)(await _systemMonitoring.GetAvailableMemoryAsync());
                
                var drives = await _systemMonitoring.GetDriveInfoAsync();
                if (drives.Any())
                {
                    server.DiskUsage = drives.Average(d => d.UsagePercent);
                }
            }
            catch
            {
                // Handle monitoring errors gracefully
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }
}