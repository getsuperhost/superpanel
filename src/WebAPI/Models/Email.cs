using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SuperPanel.WebAPI.Models
{
    public class EmailAccount
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string EmailAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Multi-tenancy: Associate email account with user
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        public int DomainId { get; set; }

        [ForeignKey("DomainId")]
        public Domain? Domain { get; set; }

        [Required]
        public long QuotaMB { get; set; } = 1024; // Default 1GB

        public long UsedQuotaMB { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<EmailForwarder> Forwarders { get; set; } = new List<EmailForwarder>();
        public virtual ICollection<EmailAlias> Aliases { get; set; } = new List<EmailAlias>();
    }

    public class EmailForwarder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EmailAccountId { get; set; }

        [ForeignKey("EmailAccountId")]
        public EmailAccount? EmailAccount { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string ForwardTo { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class EmailAlias
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EmailAccountId { get; set; }

        [ForeignKey("EmailAccountId")]
        public EmailAccount? EmailAccount { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string AliasAddress { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum EmailAccountStatus
    {
        Active = 1,
        Suspended = 2,
        Deleted = 3
    }
}