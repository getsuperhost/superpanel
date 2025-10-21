using System.ComponentModel.DataAnnotations;

namespace SuperPanel.WebAPI.Models;

public class SslCertificate
{
    public int Id { get; set; }

    [Required]
    [StringLength(255)]
    public string DomainName { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string CertificatePath { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string PrivateKeyPath { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ChainPath { get; set; }

    public SslCertificateStatus Status { get; set; } = SslCertificateStatus.Pending;

    public SslCertificateType Type { get; set; } = SslCertificateType.LetsEncrypt;

    [StringLength(500)]
    public string? Issuer { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime? LastRenewedAt { get; set; }

    public bool AutoRenew { get; set; } = true;

    [StringLength(1000)]
    public string? ValidationMethod { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    // Multi-tenancy: Associate SSL certificate with user
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public int? DomainId { get; set; }
    public virtual Domain? Domain { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum SslCertificateStatus
{
    Pending = 1,
    Active = 2,
    Expired = 3,
    Revoked = 4,
    Failed = 5
}

public enum SslCertificateType
{
    LetsEncrypt = 1,
    Custom = 2,
    SelfSigned = 3
}