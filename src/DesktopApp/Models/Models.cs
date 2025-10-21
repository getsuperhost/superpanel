namespace SuperPanel.DesktopApp.Models;

public class Server
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ServerStatus Status { get; set; } = ServerStatus.Unknown;
    public string OperatingSystem { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastChecked { get; set; }
    public List<Domain> Domains { get; set; } = new();
    public List<Database> Databases { get; set; } = new();
}

public class Domain
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DocumentRoot { get; set; }
    public DomainStatus Status { get; set; } = DomainStatus.Active;
    public bool SslEnabled { get; set; }
    public DateTime? SslExpiry { get; set; }
    public int ServerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<Subdomain> Subdomains { get; set; } = new();
}

public class Subdomain
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DomainId { get; set; }
    public string? DocumentRoot { get; set; }
    public SubdomainStatus Status { get; set; } = SubdomainStatus.Active;
    public DateTime CreatedAt { get; set; }
}

public class Database
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Username { get; set; }
    public double SizeInMB { get; set; }
    public int ServerId { get; set; }
    public DatabaseStatus Status { get; set; } = DatabaseStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? BackupDate { get; set; }
    public List<DatabaseUser> Users { get; set; } = new();
}

public class DatabaseUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public int DatabaseId { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

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
    public DateTime LastUpdated { get; set; }
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

public enum ServerStatus
{
    Unknown = 0,
    Online = 1,
    Offline = 2,
    Maintenance = 3,
    Error = 4
}

public enum DomainStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    Expired = 4
}

public enum SubdomainStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3
}

public enum DatabaseStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    Corrupted = 4
}