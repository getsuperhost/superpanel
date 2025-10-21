using System.ComponentModel.DataAnnotations;

namespace SuperPanel.WebAPI.Models;

public class Backup
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public BackupType Type { get; set; }

    [Required]
    public BackupStatus Status { get; set; } = BackupStatus.Pending;

    // Entity references (nullable for different backup types)
    public int? ServerId { get; set; }
    public virtual Server? Server { get; set; }

    public int? DatabaseId { get; set; }
    public virtual Database? Database { get; set; }

    public int? DomainId { get; set; }
    public virtual Domain? Domain { get; set; }

    // File system backup path (for full server or file backups)
    [StringLength(500)]
    public string? BackupPath { get; set; }

    // Backup file location
    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSizeInBytes { get; set; }

    // Compression and encryption
    public bool IsCompressed { get; set; } = true;
    public bool IsEncrypted { get; set; } = false;

    [StringLength(100)]
    public string? EncryptionKey { get; set; } // Reference to key, not the key itself

    // Scheduling
    public bool IsScheduled { get; set; } = false;
    [StringLength(100)]
    public string? ScheduleCron { get; set; } // Cron expression for scheduled backups

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Retention policy
    public int RetentionDays { get; set; } = 30; // Days to keep backup
    public DateTime? ExpiresAt { get; set; }

    // Error handling
    [StringLength(2000)]
    public string? ErrorMessage { get; set; }

    // Metadata
    public int CreatedByUserId { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<BackupLog> Logs { get; set; } = new List<BackupLog>();
}

public class BackupLog
{
    public int Id { get; set; }

    public int BackupId { get; set; }
    public virtual Backup Backup { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string Level { get; set; } = "Info"; // Info, Warning, Error

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [StringLength(1000)]
    public string? Details { get; set; }
}

public class BackupSchedule
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public BackupType Type { get; set; }

    // Entity references
    public int? ServerId { get; set; }
    public virtual Server? Server { get; set; }

    public int? DatabaseId { get; set; }
    public virtual Database? Database { get; set; }

    public int? DomainId { get; set; }
    public virtual Domain? Domain { get; set; }

    [Required]
    [StringLength(100)]
    public string CronExpression { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int RetentionDays { get; set; } = 30;

    public bool IsCompressed { get; set; } = true;
    public bool IsEncrypted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }

    public int CreatedByUserId { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<Backup> GeneratedBackups { get; set; } = new List<Backup>();
}

public enum BackupType
{
    FullServer = 1,      // Complete server backup
    Database = 2,        // Database backup
    Files = 3,           // File system backup
    Configuration = 4,   // Configuration files backup
    Website = 5,         // Website files backup
    Email = 6           // Email data backup
}

public enum BackupStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}