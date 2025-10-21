using System.ComponentModel.DataAnnotations;

namespace SuperPanel.WebAPI.Models;

public class Server
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    [StringLength(50)]
    public string OperatingSystem { get; set; } = string.Empty;

    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }

    // Multi-tenancy: Associate server with user
    public int UserId { get; set; }
    public virtual User? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastChecked { get; set; }

    public virtual ICollection<Domain> Domains { get; set; } = new List<Domain>();
    public virtual ICollection<Database> Databases { get; set; } = new List<Database>();
}

public enum ServerStatus
{
    Unknown = 0,
    Online = 1,
    Offline = 2,
    Maintenance = 3,
    Error = 4
}