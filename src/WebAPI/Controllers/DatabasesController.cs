using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using System.Security.Claims;

namespace SuperPanel.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DatabasesController : ControllerBase
{
    private readonly IDatabaseService _databaseService;

    public DatabasesController(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
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
    /// Get all databases (filtered by user ownership for non-admins)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllDatabases()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var databases = await _databaseService.GetAllDatabasesAsync();
            
            // Filter databases by user ownership unless user is admin
            if (!IsAdministrator())
            {
                databases = databases.Where(d => d.UserId == currentUserId).ToList();
            }
            
            return Ok(databases);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve databases", details = ex.Message });
        }
    }

    /// <summary>
    /// Get database by ID (with ownership validation)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDatabase(int id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var database = await _databaseService.GetDatabaseByIdAsync(id);
            if (database == null)
                return NotFound(new { error = "Database not found" });

            // Check ownership unless user is admin
            if (!IsAdministrator() && database.UserId != currentUserId)
                return Forbid();

            return Ok(database);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve database", details = ex.Message });
        }
    }

    /// <summary>
    /// Get databases by server ID (with ownership validation)
    /// </summary>
    [HttpGet("server/{serverId}")]
    public async Task<IActionResult> GetDatabasesByServer(int serverId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var databases = await _databaseService.GetDatabasesByServerIdAsync(serverId);
            
            // Filter databases by user ownership unless user is admin
            if (!IsAdministrator())
            {
                databases = databases.Where(d => d.UserId == currentUserId).ToList();
            }
            
            return Ok(databases);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve databases for server", details = ex.Message });
        }
    }

    /// <summary>
    /// Create a new database (assigned to current user)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateDatabase([FromBody] Database database)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            
            // Assign database to current user
            database.UserId = currentUserId;

            var createdDatabase = await _databaseService.CreateDatabaseAsync(database);
            return CreatedAtAction(nameof(GetDatabase), new { id = createdDatabase.Id }, createdDatabase);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to create database", details = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing database (with ownership validation)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDatabase(int id, [FromBody] Database database)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var existingDatabase = await _databaseService.GetDatabaseByIdAsync(id);
            
            if (existingDatabase == null)
                return NotFound(new { error = "Database not found" });

            // Check ownership unless user is admin
            if (!IsAdministrator() && existingDatabase.UserId != currentUserId)
                return Forbid();

            var updatedDatabase = await _databaseService.UpdateDatabaseAsync(id, database);
            if (updatedDatabase == null)
                return NotFound(new { error = "Database not found" });

            return Ok(updatedDatabase);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to update database", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete a database (with ownership validation)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDatabase(int id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var existingDatabase = await _databaseService.GetDatabaseByIdAsync(id);
            
            if (existingDatabase == null)
                return NotFound(new { error = "Database not found" });

            // Check ownership unless user is admin
            if (!IsAdministrator() && existingDatabase.UserId != currentUserId)
                return Forbid();

            var result = await _databaseService.DeleteDatabaseAsync(id);
            if (!result)
                return NotFound(new { error = "Database not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to delete database", details = ex.Message });
        }
    }
}