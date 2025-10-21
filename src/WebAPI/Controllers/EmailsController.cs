using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SuperPanel.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmailsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmailsController(ApplicationDbContext context)
    {
        _context = context;
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

    // GET: api/emails
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmailAccount>>> GetEmailAccounts()
    {
        var currentUserId = GetCurrentUserId();
        var emailAccounts = await _context.EmailAccounts
            .Include(e => e.Domain)
            .Include(e => e.Forwarders)
            .Include(e => e.Aliases)
            .ToListAsync();
        
        // Filter email accounts by user ownership unless user is admin
        if (!IsAdministrator())
        {
            emailAccounts = emailAccounts.Where(e => e.UserId == currentUserId).ToList();
        }
        
        return Ok(emailAccounts);

        return Ok(emailAccounts);
    }

    // GET: api/emails/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<EmailAccount>> GetEmailAccount(int id)
    {
        var emailAccount = await _context.EmailAccounts
            .Include(e => e.Domain)
            .Include(e => e.Forwarders)
            .Include(e => e.Aliases)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (emailAccount == null)
        {
            return NotFound();
        }

        return Ok(emailAccount);
    }

    // GET: api/emails/domain/{domainId}
    [HttpGet("domain/{domainId}")]
    public async Task<ActionResult<IEnumerable<EmailAccount>>> GetEmailAccountsByDomain(int domainId)
    {
        var emailAccounts = await _context.EmailAccounts
            .Include(e => e.Domain)
            .Include(e => e.Forwarders)
            .Include(e => e.Aliases)
            .Where(e => e.DomainId == domainId)
            .ToListAsync();

        return Ok(emailAccounts);
    }

    // POST: api/emails
    [HttpPost]
    public async Task<ActionResult<EmailAccount>> CreateEmailAccount([FromBody] CreateEmailAccountRequest request)
    {
        // Validate domain exists
        var domain = await _context.Domains.FindAsync(request.DomainId);
        if (domain == null)
        {
            return BadRequest("Domain not found");
        }

        // Check if email address already exists
        var existingEmail = await _context.EmailAccounts
            .FirstOrDefaultAsync(e => e.EmailAddress == request.EmailAddress);
        if (existingEmail != null)
        {
            return BadRequest("Email address already exists");
        }

        // Hash the password
        var passwordHash = HashPassword(request.Password);

        var emailAccount = new EmailAccount
        {
            EmailAddress = request.EmailAddress,
            Username = request.Username,
            PasswordHash = passwordHash,
            DomainId = request.DomainId,
            QuotaMB = request.QuotaMB,
            IsActive = true
        };

        _context.EmailAccounts.Add(emailAccount);
        await _context.SaveChangesAsync();

        // Load the domain for the response
        await _context.Entry(emailAccount).Reference(e => e.Domain).LoadAsync();

        return CreatedAtAction(nameof(GetEmailAccount), new { id = emailAccount.Id }, emailAccount);
    }

    // PUT: api/emails/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEmailAccount(int id, [FromBody] UpdateEmailAccountRequest request)
    {
        var emailAccount = await _context.EmailAccounts.FindAsync(id);
        if (emailAccount == null)
        {
            return NotFound();
        }

        // Update properties
        if (request.QuotaMB.HasValue)
        {
            emailAccount.QuotaMB = request.QuotaMB.Value;
        }

        if (request.IsActive.HasValue)
        {
            emailAccount.IsActive = request.IsActive.Value;
        }

        // Update password if provided
        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            emailAccount.PasswordHash = HashPassword(request.NewPassword);
        }

        emailAccount.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/emails/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEmailAccount(int id)
    {
        var emailAccount = await _context.EmailAccounts.FindAsync(id);
        if (emailAccount == null)
        {
            return NotFound();
        }

        _context.EmailAccounts.Remove(emailAccount);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // POST: api/emails/{id}/forwarders
    [HttpPost("{id}/forwarders")]
    public async Task<ActionResult<EmailForwarder>> AddForwarder(int id, [FromBody] CreateForwarderRequest request)
    {
        var emailAccount = await _context.EmailAccounts.FindAsync(id);
        if (emailAccount == null)
        {
            return NotFound();
        }

        var forwarder = new EmailForwarder
        {
            EmailAccountId = id,
            ForwardTo = request.ForwardTo,
            IsActive = true
        };

        _context.EmailForwarders.Add(forwarder);
        await _context.SaveChangesAsync();

        return Created("", forwarder);
    }

    // DELETE: api/emails/forwarders/{forwarderId}
    [HttpDelete("forwarders/{forwarderId}")]
    public async Task<IActionResult> DeleteForwarder(int forwarderId)
    {
        var forwarder = await _context.EmailForwarders.FindAsync(forwarderId);
        if (forwarder == null)
        {
            return NotFound();
        }

        _context.EmailForwarders.Remove(forwarder);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // POST: api/emails/{id}/aliases
    [HttpPost("{id}/aliases")]
    public async Task<ActionResult<EmailAlias>> AddAlias(int id, [FromBody] CreateAliasRequest request)
    {
        var emailAccount = await _context.EmailAccounts.FindAsync(id);
        if (emailAccount == null)
        {
            return NotFound();
        }

        var alias = new EmailAlias
        {
            EmailAccountId = id,
            AliasAddress = request.AliasAddress,
            IsActive = true
        };

        _context.EmailAliases.Add(alias);
        await _context.SaveChangesAsync();

        return Created("", alias);
    }

    // DELETE: api/emails/aliases/{aliasId}
    [HttpDelete("aliases/{aliasId}")]
    public async Task<IActionResult> DeleteAlias(int aliasId)
    {
        var alias = await _context.EmailAliases.FindAsync(aliasId);
        if (alias == null)
        {
            return NotFound();
        }

        _context.EmailAliases.Remove(alias);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}

// Request/Response DTOs
public class CreateEmailAccountRequest
{
    public string EmailAddress { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int DomainId { get; set; }
    public long QuotaMB { get; set; } = 1024;
}

public class UpdateEmailAccountRequest
{
    public long? QuotaMB { get; set; }
    public bool? IsActive { get; set; }
    public string? NewPassword { get; set; }
}

public class CreateForwarderRequest
{
    public string ForwardTo { get; set; } = string.Empty;
}

public class CreateAliasRequest
{
    public string AliasAddress { get; set; } = string.Empty;
}