using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using System.Security.Claims;

namespace SuperPanel.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DomainsController : ControllerBase
{
    private readonly IDomainService _domainService;
    private readonly IDnsService _dnsService;

    public DomainsController(IDomainService domainService, IDnsService dnsService)
    {
        _domainService = domainService;
        _dnsService = dnsService;
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

    /// <summary>
    /// Get all domains (filtered by user ownership for non-admins)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Domain>>> GetDomains()
    {
        var currentUserId = GetCurrentUserId();
        var domains = await _domainService.GetAllDomainsAsync();
        
        // Filter domains by user ownership unless user is admin
        if (!IsAdministrator())
        {
            domains = domains.Where(d => d.UserId == currentUserId).ToList();
        }
        
        return Ok(domains);
    }

    /// <summary>
    /// Get domain by ID (with ownership validation)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Domain>> GetDomain(int id)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(id);
        
        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        return Ok(domain);
    }

    /// <summary>
    /// Get domains by server ID (with ownership validation)
    /// </summary>
    [HttpGet("server/{serverId}")]
    public async Task<ActionResult<List<Domain>>> GetDomainsByServer(int serverId)
    {
        var currentUserId = GetCurrentUserId();
        var domains = await _domainService.GetDomainsByServerIdAsync(serverId);
        
        // Filter domains by user ownership unless user is admin
        if (!IsAdministrator())
        {
            domains = domains.Where(d => d.UserId == currentUserId).ToList();
        }
        
        return Ok(domains);
    }

    /// <summary>
    /// Create a new domain (assigned to current user)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Domain>> CreateDomain(Domain domain)
    {
        var currentUserId = GetCurrentUserId();
        
        // Assign domain to current user
        domain.UserId = currentUserId;
        
        var createdDomain = await _domainService.CreateDomainAsync(domain);
        return CreatedAtAction(nameof(GetDomain), new { id = createdDomain.Id }, createdDomain);
    }

    /// <summary>
    /// Update an existing domain (with ownership validation)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<Domain>> UpdateDomain(int id, Domain domain)
    {
        var currentUserId = GetCurrentUserId();
        var existingDomain = await _domainService.GetDomainByIdAsync(id);
        
        if (existingDomain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && existingDomain.UserId != currentUserId)
            return Forbid();

        var updatedDomain = await _domainService.UpdateDomainAsync(id, domain);
        if (updatedDomain == null)
            return NotFound();

        return Ok(updatedDomain);
    }

    /// <summary>
    /// Delete a domain (with ownership validation)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDomain(int id)
    {
        var currentUserId = GetCurrentUserId();
        var existingDomain = await _domainService.GetDomainByIdAsync(id);
        
        if (existingDomain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && existingDomain.UserId != currentUserId)
            return Forbid();

        var result = await _domainService.DeleteDomainAsync(id);
        if (!result)
            return NotFound();

        return NoContent();
    }

    // DNS Record endpoints

    /// <summary>
    /// Get DNS records for a domain (with ownership validation)
    /// </summary>
    [HttpGet("{domainId}/dns-records")]
    public async Task<ActionResult<List<DnsRecord>>> GetDnsRecords(int domainId)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        var records = await _dnsService.GetDnsRecordsByDomainIdAsync(domainId);
        return Ok(records);
    }

    /// <summary>
    /// Create a DNS record for a domain (with ownership validation)
    /// </summary>
    [HttpPost("{domainId}/dns-records")]
    public async Task<ActionResult<DnsRecord>> CreateDnsRecord(int domainId, DnsRecord record)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        record.DomainId = domainId;
        var createdRecord = await _dnsService.CreateDnsRecordAsync(record);
        return CreatedAtAction(nameof(GetDnsRecords), new { domainId }, createdRecord);
    }

    /// <summary>
    /// Update a DNS record (with domain ownership validation)
    /// </summary>
    [HttpPut("{domainId}/dns-records/{recordId}")]
    public async Task<ActionResult<DnsRecord>> UpdateDnsRecord(int domainId, int recordId, DnsRecord record)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        var existingRecord = await _dnsService.GetDnsRecordByIdAsync(recordId);
        if (existingRecord == null || existingRecord.DomainId != domainId)
            return NotFound();

        var updatedRecord = await _dnsService.UpdateDnsRecordAsync(recordId, record);
        if (updatedRecord == null)
            return NotFound();

        return Ok(updatedRecord);
    }

    /// <summary>
    /// Delete a DNS record (with domain ownership validation)
    /// </summary>
    [HttpDelete("{domainId}/dns-records/{recordId}")]
    public async Task<IActionResult> DeleteDnsRecord(int domainId, int recordId)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        var existingRecord = await _dnsService.GetDnsRecordByIdAsync(recordId);
        if (existingRecord == null || existingRecord.DomainId != domainId)
            return NotFound();

        var result = await _dnsService.DeleteDnsRecordAsync(recordId);
        if (!result)
            return NotFound();

        return NoContent();
    }

    // DNS Zone endpoints

    /// <summary>
    /// Get DNS zone for a domain (with ownership validation)
    /// </summary>
    [HttpGet("{domainId}/dns-zone")]
    public async Task<ActionResult<DnsZone>> GetDnsZone(int domainId)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        var zone = await _dnsService.GetDnsZoneByDomainIdAsync(domainId);
        if (zone == null)
            return NotFound();

        return Ok(zone);
    }

    /// <summary>
    /// Create or update DNS zone for a domain (with ownership validation)
    /// </summary>
    [HttpPut("{domainId}/dns-zone")]
    public async Task<ActionResult<DnsZone>> UpdateDnsZone(int domainId, DnsZone zone)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        var updatedZone = await _dnsService.CreateOrUpdateDnsZoneAsync(domainId, zone);
        return Ok(updatedZone);
    }

    /// <summary>
    /// Generate zone file for a domain (with ownership validation)
    /// </summary>
    [HttpGet("{domainId}/dns-zone/generate")]
    public async Task<ActionResult<string>> GenerateZoneFile(int domainId)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        var zoneFile = await _dnsService.GenerateZoneFileAsync(domainId);
        if (zoneFile == null)
            return NotFound();

        return Ok(zoneFile);
    }

    /// <summary>
    /// Validate zone file content
    /// </summary>
    [HttpPost("dns-zone/validate")]
    public async Task<ActionResult<bool>> ValidateZoneFile([FromBody] string zoneFile)
    {
        var isValid = await _dnsService.ValidateZoneFileAsync(zoneFile);
        return Ok(isValid);
    }

    // DNS Propagation endpoints

    /// <summary>
    /// Get DNS propagation status for a domain (with ownership validation)
    /// </summary>
    [HttpGet("{domainId}/dns-propagation")]
    public async Task<ActionResult<DnsPropagationStatus>> GetDnsPropagationStatus(int domainId)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        var status = await _dnsService.GetDnsPropagationStatusByDomainIdAsync(domainId);
        if (status == null)
            return NotFound();

        return Ok(status);
    }

    /// <summary>
    /// Check DNS propagation status for a domain (with ownership validation)
    /// </summary>
    [HttpPost("{domainId}/dns-propagation/check")]
    public async Task<ActionResult<DnsPropagationStatus>> CheckDnsPropagation(int domainId)
    {
        var currentUserId = GetCurrentUserId();
        var domain = await _domainService.GetDomainByIdAsync(domainId);

        if (domain == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && domain.UserId != currentUserId)
            return Forbid();

        var status = await _dnsService.CheckDnsPropagationAsync(domainId);
        return Ok(status);
    }
}