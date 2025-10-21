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

    public DomainsController(IDomainService domainService)
    {
        _domainService = domainService;
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
}