namespace SuperPanel.WebAPI.Models;

public class SystemInfo
{
    public string ServerName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public long TotalMemoryMB { get; set; }
    public long AvailableMemoryMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public List<DriveInfo> Drives { get; set; } = new();
    public List<ProcessInfo> TopProcesses { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class DriveInfo
{
    public string Name { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long TotalSizeGB { get; set; }
    public long AvailableSpaceGB { get; set; }
    public double UsagePercent { get; set; }
}

public class ProcessInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public long MemoryMB { get; set; }
}

public class FileSystemItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public string Permissions { get; set; } = string.Empty;
}