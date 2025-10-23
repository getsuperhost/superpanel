using System.ComponentModel.DataAnnotations;

namespace SuperPanel.WebAPI.Models;

public class Domain
{
    public int Id { get; set; }

    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? DocumentRoot { get; set; }

    public DomainStatus Status { get; set; } = DomainStatus.Active;

    public bool SslEnabled { get; set; }
    public DateTime? SslExpiry { get; set; }

    // Multi-tenancy: Associate domain with user
    public int UserId { get; set; }
    public virtual User? User { get; set; }

    public int ServerId { get; set; }
    public virtual Server? Server { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Subdomain> Subdomains { get; set; } = new List<Subdomain>();

    public virtual ICollection<DnsRecord> DnsRecords { get; set; } = new List<DnsRecord>();
    public virtual DnsZone? DnsZone { get; set; }
    public virtual DnsPropagationStatus? DnsPropagationStatus { get; set; }
}

public class Subdomain
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public int DomainId { get; set; }
    public virtual Domain Domain { get; set; } = null!;
    
    [StringLength(500)]
    public string? DocumentRoot { get; set; }
    
    public SubdomainStatus Status { get; set; } = SubdomainStatus.Active;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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