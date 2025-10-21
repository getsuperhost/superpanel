using System.Management;
using System.Diagnostics;
using SuperPanel.DesktopApp.Models;

namespace SuperPanel.DesktopApp.Services;

public interface ISystemService
{
    Task<SystemInfo> GetLocalSystemInfoAsync();
    Task<List<ProcessInfo>> GetTopProcessesAsync(int count = 10);
    Task<double> GetCpuUsageAsync();
    Task<long> GetAvailableMemoryAsync();
}

public class SystemService : ISystemService
{
    public async Task<SystemInfo> GetLocalSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var systemInfo = new SystemInfo
            {
                ServerName = Environment.MachineName,
                OperatingSystem = Environment.OSVersion.ToString(),
                Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                LastUpdated = DateTime.UtcNow
            };

            try
            {
                // Get memory information
                var memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                systemInfo.AvailableMemoryMB = (long)memoryCounter.NextValue();

                // Get total physical memory
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        systemInfo.TotalMemoryMB = Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024);
                        break;
                    }
                }

                // Get CPU usage
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call returns 0
                Thread.Sleep(100);
                systemInfo.CpuUsagePercent = Math.Round(cpuCounter.NextValue(), 2);

                // Get drive information
                systemInfo.Drives = GetDriveInfo();

                // Get top processes
                systemInfo.TopProcesses = GetTopProcesses(10).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting system info: {ex.Message}");
            }

            return systemInfo;
        });
    }

    public async Task<List<ProcessInfo>> GetTopProcessesAsync(int count = 10)
    {
        return await GetTopProcesses(count);
    }

    public async Task<double> GetCpuUsageAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call returns 0
                Thread.Sleep(100);
                return Math.Round(cpuCounter.NextValue(), 2);
            }
            catch
            {
                return 0.0;
            }
        });
    }

    public async Task<long> GetAvailableMemoryAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                return (long)memoryCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        });
    }

    private async Task<List<ProcessInfo>> GetTopProcesses(int count)
    {
        return await Task.Run(() =>
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => !p.HasExited)
                    .OrderByDescending(p => p.WorkingSet64)
                    .Take(count)
                    .Select(p => new ProcessInfo
                    {
                        Id = p.Id,
                        Name = p.ProcessName,
                        MemoryMB = p.WorkingSet64 / (1024 * 1024),
                        CpuPercent = 0 // Would need performance counters for accurate CPU usage per process
                    })
                    .ToList();

                return processes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting top processes: {ex.Message}");
                return new List<ProcessInfo>();
            }
        });
    }

    private List<Models.DriveInfo> GetDriveInfo()
    {
        var drives = new List<Models.DriveInfo>();

        try
        {
            var systemDrives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .ToList();

            foreach (var drive in systemDrives)
            {
                var totalSizeGB = drive.TotalSize / (1024L * 1024 * 1024);
                var availableSpaceGB = drive.AvailableFreeSpace / (1024L * 1024 * 1024);
                var usagePercent = Math.Round((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100, 2);

                drives.Add(new Models.DriveInfo
                {
                    Name = drive.Name,
                    FileSystem = drive.DriveFormat,
                    TotalSizeGB = totalSizeGB,
                    AvailableSpaceGB = availableSpaceGB,
                    UsagePercent = usagePercent
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting drive info: {ex.Message}");
        }

        return drives;
    }
}