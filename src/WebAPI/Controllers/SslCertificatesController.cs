using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using System.Security.Claims;

namespace SuperPanel.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SslCertificatesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISslCertificateService _sslService;

    public SslCertificatesController(ApplicationDbContext context, ISslCertificateService sslService)
    {
        _context = context;
        _sslService = sslService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user token");
        }
        return userId;
    }

    private bool IsAdministrator()
    {
        return User.IsInRole("Administrator");
    }

    // GET: api/ssl-certificates
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SslCertificate>>> GetSslCertificates()
    {
        var currentUserId = GetCurrentUserId();
        var certificates = await _context.SslCertificates
            .Include(c => c.Domain)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        
        // Filter SSL certificates by user ownership unless user is admin
        if (!IsAdministrator())
        {
            certificates = certificates.Where(c => c.UserId == currentUserId).ToList();
        }

        return Ok(certificates);
    }

    // GET: api/ssl-certificates/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<SslCertificate>> GetSslCertificate(int id)
    {
        var certificate = await _context.SslCertificates
            .Include(c => c.Domain)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (certificate == null)
        {
            return NotFound();
        }

        return Ok(certificate);
    }

    // GET: api/ssl-certificates/domain/{domainId}
    [HttpGet("domain/{domainId}")]
    public async Task<ActionResult<IEnumerable<SslCertificate>>> GetCertificatesByDomain(int domainId)
    {
        var certificates = await _context.SslCertificates
            .Where(c => c.DomainId == domainId)
            .Include(c => c.Domain)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(certificates);
    }

    // GET: api/ssl-certificates/expiring-soon
    [HttpGet("expiring-soon")]
    public async Task<ActionResult<IEnumerable<SslCertificate>>> GetExpiringSoonCertificates([FromQuery] int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(days);

        var certificates = await _context.SslCertificates
            .Where(c => c.Status == SslCertificateStatus.Active && c.ExpiresAt <= cutoffDate)
            .Include(c => c.Domain)
            .OrderBy(c => c.ExpiresAt)
            .ToListAsync();

        return Ok(certificates);
    }

    // POST: api/ssl-certificates/request
    [HttpPost("request")]
    public async Task<ActionResult<SslCertificate>> RequestCertificate([FromBody] CertificateRequest request)
    {
        // Validate domain exists
        var domain = await _context.Domains.FindAsync(request.DomainId);
        if (domain == null)
        {
            return BadRequest("Domain not found");
        }

        // Check if certificate already exists for this domain
        var existingCertificate = await _context.SslCertificates
            .FirstOrDefaultAsync(c => c.DomainName == domain.Name && c.Status == SslCertificateStatus.Active);

        if (existingCertificate != null)
        {
            return BadRequest("Active certificate already exists for this domain");
        }

        var certificate = new SslCertificate
        {
            DomainName = domain.Name,
            DomainId = request.DomainId,
            Status = SslCertificateStatus.Pending,
            Type = request.Type,
            AutoRenew = request.AutoRenew,
            ValidationMethod = request.ValidationMethod,
            Notes = request.Notes,
            ExpiresAt = DateTime.UtcNow.AddDays(90) // Default 90 days for Let's Encrypt
        };

        _context.SslCertificates.Add(certificate);
        await _context.SaveChangesAsync();

        // Trigger certificate generation process
        await _sslService.RequestCertificateAsync(certificate.Id);

        return CreatedAtAction(nameof(GetSslCertificate), new { id = certificate.Id }, certificate);
    }

    // PUT: api/ssl-certificates/{id}/install
    [HttpPut("{id}/install")]
    public async Task<IActionResult> InstallCertificate(int id, [FromBody] CertificateInstallRequest request)
    {
        var certificate = await _context.SslCertificates.FindAsync(id);
        if (certificate == null)
        {
            return NotFound();
        }

        certificate.CertificatePath = request.CertificatePath;
        certificate.PrivateKeyPath = request.PrivateKeyPath;
        certificate.ChainPath = request.ChainPath;
        certificate.Status = SslCertificateStatus.Active;
        certificate.Issuer = request.Issuer;
        certificate.IssuedAt = DateTime.UtcNow;
        certificate.ExpiresAt = request.ExpiresAt;
        certificate.LastRenewedAt = DateTime.UtcNow;
        certificate.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Update domain SSL status
        if (certificate.DomainId.HasValue)
        {
            var domain = await _context.Domains.FindAsync(certificate.DomainId.Value);
            if (domain != null)
            {
                domain.SslEnabled = true;
                domain.SslExpiry = certificate.ExpiresAt;
                domain.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        return NoContent();
    }

    // PUT: api/ssl-certificates/{id}/renew
    [HttpPut("{id}/renew")]
    public async Task<IActionResult> RenewCertificate(int id)
    {
        var certificate = await _context.SslCertificates.FindAsync(id);
        if (certificate == null)
        {
            return NotFound();
        }

        // Trigger certificate renewal process
        await _sslService.RenewCertificateAsync(id);

        certificate.Status = SslCertificateStatus.Pending;
        certificate.LastRenewedAt = DateTime.UtcNow;
        certificate.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/ssl-certificates/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteCertificate(int id)
    {
        var certificate = await _context.SslCertificates.FindAsync(id);
        if (certificate == null)
        {
            return NotFound();
        }

        // Update domain SSL status
        if (certificate.DomainId.HasValue)
        {
            var domain = await _context.Domains.FindAsync(certificate.DomainId.Value);
            if (domain != null)
            {
                domain.SslEnabled = false;
                domain.SslExpiry = null;
                domain.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        _context.SslCertificates.Remove(certificate);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // POST: api/ssl-certificates/provision
    [HttpPost("provision")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<IEnumerable<SslCertificate>>> ProvisionCertificates([FromBody] ProvisionRequest request)
    {
        var domains = await _context.Domains
            .Where(d => d.SslEnabled == false || d.SslExpiry == null || d.SslExpiry <= DateTime.UtcNow.AddDays(request.DaysBeforeExpiry))
            .ToListAsync();

        var results = new List<SslCertificate>();

        foreach (var domain in domains)
        {
            // Check if certificate already exists and is active
            var existingCertificate = await _context.SslCertificates
                .FirstOrDefaultAsync(c => c.DomainName == domain.Name && c.Status == SslCertificateStatus.Active);

            if (existingCertificate != null)
            {
                // If certificate exists but domain SSL is not enabled, update domain
                if (!domain.SslEnabled)
                {
                    domain.SslEnabled = true;
                    domain.SslExpiry = existingCertificate.ExpiresAt;
                    domain.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                continue;
            }

            // Create new certificate request
            var certificate = new SslCertificate
            {
                DomainName = domain.Name,
                DomainId = domain.Id,
                Status = SslCertificateStatus.Pending,
                Type = SslCertificateType.LetsEncrypt,
                AutoRenew = true,
                ValidationMethod = "http-01",
                Notes = "Auto-provisioned",
                ExpiresAt = DateTime.UtcNow.AddDays(90)
            };

            _context.SslCertificates.Add(certificate);
            await _context.SaveChangesAsync();

            // Trigger certificate generation
            await _sslService.RequestCertificateAsync(certificate.Id);

            results.Add(certificate);
        }

        return Ok(results);
    }

    // POST: api/ssl-certificates/renew-expiring
    [HttpPost("renew-expiring")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<IEnumerable<SslCertificate>>> RenewExpiringCertificates([FromQuery] int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(days);

        var certificates = await _context.SslCertificates
            .Where(c => c.Status == SslCertificateStatus.Active && c.ExpiresAt <= cutoffDate && c.Type == SslCertificateType.LetsEncrypt)
            .ToListAsync();

        var results = new List<SslCertificate>();

        foreach (var certificate in certificates)
        {
            // Trigger renewal
            await _sslService.RenewCertificateAsync(certificate.Id);

            certificate.Status = SslCertificateStatus.Pending;
            certificate.LastRenewedAt = DateTime.UtcNow;
            certificate.UpdatedAt = DateTime.UtcNow;

            results.Add(certificate);
        }

        await _context.SaveChangesAsync();

        return Ok(results);
    }

    // GET: api/ssl-certificates/provisioning-status
    [HttpGet("provisioning-status")]
    public async Task<ActionResult<ProvisioningStatus>> GetProvisioningStatus()
    {
        var totalCertificates = await _context.SslCertificates.CountAsync();
        var activeCertificates = await _context.SslCertificates.CountAsync(c => c.Status == SslCertificateStatus.Active);
        var pendingCertificates = await _context.SslCertificates.CountAsync(c => c.Status == SslCertificateStatus.Pending);
        var failedCertificates = await _context.SslCertificates.CountAsync(c => c.Status == SslCertificateStatus.Failed);
        var expiringSoon = await _context.SslCertificates.CountAsync(c => c.Status == SslCertificateStatus.Active && c.ExpiresAt <= DateTime.UtcNow.AddDays(30));

        var domainsWithoutSsl = await _context.Domains.CountAsync(d => d.SslEnabled == false);
        var domainsWithExpiredSsl = await _context.Domains.CountAsync(d => d.SslEnabled == true && d.SslExpiry <= DateTime.UtcNow);

        return Ok(new ProvisioningStatus
        {
            TotalCertificates = totalCertificates,
            ActiveCertificates = activeCertificates,
            PendingCertificates = pendingCertificates,
            FailedCertificates = failedCertificates,
            ExpiringSoonCertificates = expiringSoon,
            DomainsWithoutSsl = domainsWithoutSsl,
            DomainsWithExpiredSsl = domainsWithExpiredSsl
        });
    }
}

// DTOs
public class CertificateRequest
{
    public int DomainId { get; set; }
    public SslCertificateType Type { get; set; } = SslCertificateType.LetsEncrypt;
    public bool AutoRenew { get; set; } = true;
    public string? ValidationMethod { get; set; }
    public string? Notes { get; set; }
}

public class CertificateInstallRequest
{
    public string CertificatePath { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string? ChainPath { get; set; }
    public string? Issuer { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class ProvisionRequest
{
    public int DaysBeforeExpiry { get; set; } = 30;
}

public class ProvisioningStatus
{
    public int TotalCertificates { get; set; }
    public int ActiveCertificates { get; set; }
    public int PendingCertificates { get; set; }
    public int FailedCertificates { get; set; }
    public int ExpiringSoonCertificates { get; set; }
    public int DomainsWithoutSsl { get; set; }
    public int DomainsWithExpiredSsl { get; set; }
}