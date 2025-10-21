using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using System.Security.Claims;

namespace SuperPanel.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServersController : ControllerBase
{
    private readonly IServerService _serverService;
    private readonly ISystemMonitoringService _systemMonitoring;

    public ServersController(IServerService serverService, ISystemMonitoringService systemMonitoring)
    {
        _serverService = serverService;
        _systemMonitoring = systemMonitoring;
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
    /// Get all servers (filtered by user ownership for non-admins)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Server>>> GetServers()
    {
        var currentUserId = GetCurrentUserId();
        var servers = await _serverService.GetAllServersAsync();
        
        // Filter servers by user ownership unless user is admin
        if (!IsAdministrator())
        {
            servers = servers.Where(s => s.UserId == currentUserId).ToList();
        }
        
        return Ok(servers);
    }

    /// <summary>
    /// Get server by ID (with ownership validation)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Server>> GetServer(int id)
    {
        var currentUserId = GetCurrentUserId();
        var server = await _serverService.GetServerByIdAsync(id);
        
        if (server == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && server.UserId != currentUserId)
            return Forbid();

        return Ok(server);
    }

    /// <summary>
    /// Create a new server (assigned to current user)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Server>> CreateServer(Server server)
    {
        var currentUserId = GetCurrentUserId();
        
        // Assign server to current user
        server.UserId = currentUserId;
        
        var createdServer = await _serverService.CreateServerAsync(server);
        return CreatedAtAction(nameof(GetServer), new { id = createdServer.Id }, createdServer);
    }

    /// <summary>
    /// Update an existing server (with ownership validation)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<Server>> UpdateServer(int id, Server server)
    {
        var currentUserId = GetCurrentUserId();
        var existingServer = await _serverService.GetServerByIdAsync(id);
        
        if (existingServer == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && existingServer.UserId != currentUserId)
            return Forbid();

        var updatedServer = await _serverService.UpdateServerAsync(id, server);
        if (updatedServer == null)
            return NotFound();

        return Ok(updatedServer);
    }

    /// <summary>
    /// Delete a server (with ownership validation)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteServer(int id)
    {
        var currentUserId = GetCurrentUserId();
        var existingServer = await _serverService.GetServerByIdAsync(id);
        
        if (existingServer == null)
            return NotFound();

        // Check ownership unless user is admin
        if (!IsAdministrator() && existingServer.UserId != currentUserId)
            return Forbid();

        var result = await _serverService.DeleteServerAsync(id);
        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Update server status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateServerStatus(int id, [FromBody] ServerStatus status)
    {
        var result = await _serverService.UpdateServerStatusAsync(id, status);
        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get current system information
    /// </summary>
    [HttpGet("system-info")]
    public async Task<ActionResult<SystemInfo>> GetSystemInfo()
    {
        var systemInfo = await _systemMonitoring.GetSystemInfoAsync();
        return Ok(systemInfo);
    }
}