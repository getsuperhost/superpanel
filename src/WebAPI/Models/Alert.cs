using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SuperPanel.WebAPI.Models
{
    public class AlertRule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public AlertRuleType Type { get; set; }

        // Multi-tenancy: Associate alert rule with user
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // For server-specific rules
        public int? ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual Server? Server { get; set; }

        // For metric-based rules
        [StringLength(50)]
        public string? MetricName { get; set; }

        [Required]
        [StringLength(2)]
        public string Condition { get; set; } // gt, lt, eq, ne

        [Required]
        public double Threshold { get; set; }

        [Required]
        public AlertRuleSeverity Severity { get; set; }

        [Required]
        public bool Enabled { get; set; } = true;

        [Required]
        public int CooldownMinutes { get; set; } = 5;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Notification settings
        [StringLength(500)]
        public string? WebhookUrl { get; set; }

        [StringLength(1000)]
        public string? EmailRecipients { get; set; } // JSON array of emails

        [StringLength(500)]
        public string? SlackWebhookUrl { get; set; }

        // Navigation properties
        public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    }

    public class Alert
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AlertRuleId { get; set; }
        [ForeignKey("AlertRuleId")]
        public virtual AlertRule? AlertRule { get; set; }

        [Required]
        public int ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual Server? Server { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        [StringLength(1000)]
        public string Message { get; set; }

        [Required]
        public AlertRuleSeverity Severity { get; set; }

        [Required]
        public AlertStatus Status { get; set; } = AlertStatus.Active;

        [Required]
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        public DateTime? AcknowledgedAt { get; set; }

        // Metric values at time of alert
        public double? MetricValue { get; set; }

        [StringLength(50)]
        public string MetricName { get; set; }

        // Notification tracking
        public bool NotificationSent { get; set; } = false;
        public DateTime? LastNotificationSent { get; set; }

        // Additional context
        [StringLength(2000)]
        public string ContextData { get; set; } // JSON string with additional data

        // Navigation properties
        public virtual ICollection<AlertHistory> AlertHistories { get; set; } = new List<AlertHistory>();
        public virtual ICollection<AlertComment> AlertComments { get; set; } = new List<AlertComment>();
    }

    public class AlertHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AlertId { get; set; }
        [ForeignKey("AlertId")]
        public virtual Alert Alert { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; } // "Created", "Acknowledged", "Resolved", "StatusChanged"

        [Required]
        public AlertStatus OldStatus { get; set; }

        [Required]
        public AlertStatus NewStatus { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Who performed the action (could be user ID or system)
        [StringLength(100)]
        public string PerformedBy { get; set; } = "System";
    }

    public class AlertComment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AlertId { get; set; }
        [ForeignKey("AlertId")]
        public virtual Alert Alert { get; set; }

        [Required]
        [StringLength(1000)]
        public string Comment { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Who added the comment
        [StringLength(100)]
        public string CreatedBy { get; set; } = "System";

        [StringLength(50)]
        public string CommentType { get; set; } = "General"; // "Acknowledgment", "Resolution", "General"
    }

    public enum AlertRuleType
    {
        ServerDown = 1,
        HighCpuUsage = 2,
        HighMemoryUsage = 3,
        LowDiskSpace = 4,
        HighNetworkUsage = 5,
        ServiceUnavailable = 6,
        CustomMetric = 7
    }

    public enum AlertRuleSeverity
    {
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    public enum AlertStatus
    {
        Active = 1,
        Acknowledged = 2,
        Resolved = 3,
        Suppressed = 4
    }
}