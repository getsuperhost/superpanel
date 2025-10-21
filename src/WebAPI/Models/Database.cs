using System.ComponentModel.DataAnnotations;

namespace SuperPanel.WebAPI.Models;

public class Database
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty; // MySQL, PostgreSQL, SQL Server, etc.

    [StringLength(100)]
    public string? Username { get; set; }

    public double SizeInMB { get; set; }

    // Multi-tenancy: Associate database with user
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public int ServerId { get; set; }
    public virtual Server Server { get; set; } = null!;

    public DatabaseStatus Status { get; set; } = DatabaseStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? BackupDate { get; set; }

    public virtual ICollection<DatabaseUser> Users { get; set; } = new List<DatabaseUser>();
}

public class DatabaseUser
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;
    
    public int DatabaseId { get; set; }
    public virtual Database Database { get; set; } = null!;
    
    [StringLength(500)]
    public string Permissions { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum DatabaseStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    Corrupted = 4
}