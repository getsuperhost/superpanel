using System.ComponentModel.DataAnnotations;

namespace SuperPanel.WebAPI.Models;

public class DnsRecord
{
    public int Id { get; set; }

    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    public DnsRecordType Type { get; set; }

    [Required]
    [StringLength(1000)]
    public string Value { get; set; } = string.Empty;

    public int Ttl { get; set; } = 3600; // Default 1 hour

    public int Priority { get; set; } // For MX records

    public DnsRecordStatus Status { get; set; } = DnsRecordStatus.Active;

    public int DomainId { get; set; }
    public virtual Domain Domain { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class DnsZone
{
    public int Id { get; set; }

    public int DomainId { get; set; }
    public virtual Domain Domain { get; set; } = null!;

    [StringLength(1000)]
    public string? ZoneFile { get; set; }

    public bool AutoUpdate { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}

public class DnsPropagationStatus
{
    public int Id { get; set; }

    public int DomainId { get; set; }
    public virtual Domain Domain { get; set; } = null!;

    public PropagationState State { get; set; } = PropagationState.Pending;

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [StringLength(1000)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum DnsRecordType
{
    A = 1,
    AAAA = 2,
    CNAME = 3,
    MX = 4,
    TXT = 5,
    SRV = 6,
    PTR = 7,
    NS = 8,
    SOA = 9
}

public enum DnsRecordStatus
{
    Active = 1,
    Inactive = 2,
    Pending = 3,
    Error = 4
}

public enum PropagationState
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4
}