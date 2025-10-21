using System.Runtime.InteropServices;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public interface ISystemMonitoringService
{
    Task<SystemInfo> GetSystemInfoAsync();
    Task<List<ProcessInfo>> GetTopProcessesAsync(int count = 10);
    Task<double> GetCpuUsageAsync();
    Task<long> GetAvailableMemoryAsync();
    Task<long> GetTotalMemoryAsync();
    Task<List<Models.DriveInfo>> GetDriveInfoAsync();
}

public class SystemMonitoringService : ISystemMonitoringService
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly bool NativeLibraryAvailable = IsWindows && File.Exists("SuperPanel.NativeLibrary.dll");

    // Import from native library (Windows only)
    [DllImport("SuperPanel.NativeLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern double GetCpuUsage();

    [DllImport("SuperPanel.NativeLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern long GetAvailableMemory();

    [DllImport("SuperPanel.NativeLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern long GetTotalMemory();

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var systemInfo = new SystemInfo
        {
            ServerName = Environment.MachineName,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            CpuUsagePercent = await GetCpuUsageAsync(),
            TotalMemoryMB = await GetTotalMemoryAsync(),
            AvailableMemoryMB = await GetAvailableMemoryAsync(),
            Drives = await GetDriveInfoAsync(),
            TopProcesses = await GetTopProcessesAsync(),
            LastUpdated = DateTime.UtcNow
        };

        return systemInfo;
    }

    public async Task<List<ProcessInfo>> GetTopProcessesAsync(int count = 10)
    {
        return await Task.Run(() =>
        {
            var processes = System.Diagnostics.Process.GetProcesses()
                .Where(p => !p.HasExited && p.ProcessName != "Idle")
                .OrderByDescending(p => p.WorkingSet64)
                .Take(count)
                .Select(p => new ProcessInfo
                {
                    Id = p.Id,
                    Name = p.ProcessName ?? "Unknown",
                    MemoryMB = Math.Max(0, p.WorkingSet64 / (1024 * 1024)),
                    CpuPercent = 0.0 // Would need performance counters for accurate CPU usage
                })
                .ToList();

            return processes;
        });
    }

    public async Task<double> GetCpuUsageAsync()
    {
        return await Task.Run(() =>
        {
            if (NativeLibraryAvailable)
            {
                try
                {
                    return GetCpuUsage();
                }
                catch
                {
                    // Fall through to .NET implementation
                }
            }
            
            // Cross-platform .NET implementation
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;
                
                Thread.Sleep(100);
                
                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                
                if (totalMsPassed <= 0 || double.IsInfinity(cpuUsedMs) || double.IsNaN(cpuUsedMs))
                    return 0.0;
                    
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                var result = cpuUsageTotal * 100;
                
                // Ensure we return a valid number
                if (double.IsInfinity(result) || double.IsNaN(result) || result < 0)
                    return 0.0;
                if (result > 100)
                    return 100.0;
                    
                return result;
            }
        });
    }

    public async Task<long> GetAvailableMemoryAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                return GetAvailableMemory() / (1024 * 1024); // Convert to MB
            }
            catch
            {
                // Fallback implementation
                var memory = GC.GetTotalMemory(false) / (1024 * 1024);
                return memory > 0 ? memory : 1024; // Fallback to 1GB
            }
        });
    }

    public async Task<long> GetTotalMemoryAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                return GetTotalMemory() / (1024 * 1024); // Convert to MB
            }
            catch
            {
                // Fallback - this is an approximation
                return 8192; // Default to 8GB
            }
        });
    }

    public async Task<List<Models.DriveInfo>> GetDriveInfoAsync()
    {
        return await Task.Run(() =>
        {
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new Models.DriveInfo
                {
                    Name = d.Name,
                    FileSystem = d.DriveFormat,
                    TotalSizeGB = d.TotalSize / (1024 * 1024 * 1024),
                    AvailableSpaceGB = d.AvailableFreeSpace / (1024 * 1024 * 1024),
                    UsagePercent = d.TotalSize > 0 ? Math.Round((double)(d.TotalSize - d.AvailableFreeSpace) / d.TotalSize * 100, 2) : 0.0
                })
                .ToList();

            return drives;
        });
    }
}