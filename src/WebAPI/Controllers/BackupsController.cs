using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperPanel.WebAPI.Services;
using SuperPanel.WebAPI.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace SuperPanel.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BackupsController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly ILogger<BackupsController> _logger;

    public BackupsController(IBackupService backupService, ILogger<BackupsController> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    private bool IsAdministrator()
    {
        return User.IsInRole("Administrator");
    }

    /// <summary>
    /// Get all backups with optional filtering (filtered by user ownership for non-admins)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BackupDto>>> GetBackups(
        [FromQuery] int? serverId,
        [FromQuery] int? databaseId,
        [FromQuery] int? domainId)
    {
        try
        {
            var backups = await _backupService.GetBackupsAsync(serverId, databaseId, domainId);
            var backupDtos = backups.Select(MapToDto);
            return Ok(backupDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backups");
            return StatusCode(500, new { message = "Error retrieving backups", error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific backup by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BackupDto>> GetBackup(int id)
    {
        try
        {
            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null)
                return NotFound(new { message = "Backup not found" });

            return Ok(MapToDto(backup));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backup {Id}", id);
            return StatusCode(500, new { message = "Error retrieving backup", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new backup
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BackupDto>> CreateBackup([FromBody] CreateBackupRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Get user ID from claims (assuming JWT authentication)
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Invalid user authentication" });

            var backupRequest = new BackupRequest
            {
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                ServerId = request.ServerId,
                DatabaseId = request.DatabaseId,
                DomainId = request.DomainId,
                BackupPath = request.BackupPath,
                IsCompressed = request.IsCompressed,
                IsEncrypted = request.IsEncrypted,
                RetentionDays = request.RetentionDays
            };

            var backup = await _backupService.CreateBackupAsync(backupRequest, userId);
            var backupDto = MapToDto(backup);

            return CreatedAtAction(nameof(GetBackup), new { id = backup.Id }, backupDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
            return StatusCode(500, new { message = "Error creating backup", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a backup
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBackup(int id)
    {
        try
        {
            var result = await _backupService.DeleteBackupAsync(id);
            if (!result)
                return NotFound(new { message = "Backup not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup {Id}", id);
            return StatusCode(500, new { message = "Error deleting backup", error = ex.Message });
        }
    }

    /// <summary>
    /// Restore from a backup
    /// </summary>
    [HttpPost("{id}/restore")]
    public async Task<ActionResult<RestoreResult>> RestoreBackup(int id, [FromBody] RestoreBackupRequest request)
    {
        try
        {
            var restoreRequest = new RestoreRequest
            {
                RestorePath = request.RestorePath,
                OverwriteExisting = request.OverwriteExisting
            };

            var result = await _backupService.RestoreBackupAsync(id, restoreRequest);
            if (result.Success)
                return Ok(result);
            else
                return BadRequest(result);
        }
        catch (ArgumentException ex)
        {
            // Domain-level 'not found' or invalid identifier
            _logger.LogWarning(ex, "Restore requested for non-existent backup {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Domain-level invalid operation (e.g. wrong state)
            _logger.LogWarning(ex, "Invalid restore operation for backup {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup {Id}", id);
            return StatusCode(500, new { message = "Error restoring backup", error = ex.Message });
        }
    }

    /// <summary>
    /// Download a backup file
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadBackup(int id)
    {
        try
        {
            var backup = await _backupService.GetBackupAsync(id);
            if (backup == null)
                return NotFound(new { message = "Backup not found" });

            if (backup.Status != BackupStatus.Completed)
                return BadRequest(new { message = "Backup is not ready for download" });

            if (!System.IO.File.Exists(backup.FilePath))
                return NotFound(new { message = "Backup file not found" });

            var fileBytes = await System.IO.File.ReadAllBytesAsync(backup.FilePath);
            var fileName = $"{backup.Name}_{backup.CreatedAt:yyyyMMdd_HHmmss}{Path.GetExtension(backup.FilePath)}";

            return File(fileBytes, "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading backup {Id}", id);
            return StatusCode(500, new { message = "Error downloading backup", error = ex.Message });
        }
    }

    private static BackupDto MapToDto(Backup backup)
    {
        return new BackupDto
        {
            Id = backup.Id,
            Name = backup.Name,
            Description = backup.Description,
            Type = backup.Type,
            Status = backup.Status,
            ServerId = backup.ServerId,
            ServerName = backup.Server?.Name,
            DatabaseId = backup.DatabaseId,
            DatabaseName = backup.Database?.Name,
            DomainId = backup.DomainId,
            DomainName = backup.Domain?.Name,
            BackupPath = backup.BackupPath,
            FilePath = backup.FilePath,
            FileSizeInBytes = backup.FileSizeInBytes,
            IsCompressed = backup.IsCompressed,
            IsEncrypted = backup.IsEncrypted,
            IsScheduled = backup.IsScheduled,
            RetentionDays = backup.RetentionDays,
            CreatedAt = backup.CreatedAt,
            StartedAt = backup.StartedAt,
            CompletedAt = backup.CompletedAt,
            ExpiresAt = backup.ExpiresAt,
            ErrorMessage = backup.ErrorMessage,
            CreatedByUserId = backup.CreatedByUserId,
            CreatedByUsername = backup.CreatedByUser?.Username,
            Logs = backup.Logs?.Select(l => new BackupLogDto
            {
                Id = l.Id,
                Level = l.Level,
                Message = l.Message,
                Details = l.Details,
                Timestamp = l.Timestamp
            }).ToList() ?? new List<BackupLogDto>()
        };
    }
}

// DTOs for API requests and responses
public class CreateBackupRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BackupType Type { get; set; }
    public int? ServerId { get; set; }
    public int? DatabaseId { get; set; }
    public int? DomainId { get; set; }
    public string? BackupPath { get; set; }
    public bool IsCompressed { get; set; } = true;
    public bool IsEncrypted { get; set; } = false;
    public int RetentionDays { get; set; } = 30;
}

public class RestoreBackupRequest
{
    public string? RestorePath { get; set; }
    public bool OverwriteExisting { get; set; } = false;
}

public class BackupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BackupType Type { get; set; }
    public BackupStatus Status { get; set; }
    public int? ServerId { get; set; }
    public string? ServerName { get; set; }
    public int? DatabaseId { get; set; }
    public string? DatabaseName { get; set; }
    public int? DomainId { get; set; }
    public string? DomainName { get; set; }
    public string? BackupPath { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public bool IsCompressed { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsScheduled { get; set; }
    public int RetentionDays { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByUsername { get; set; }
    public List<BackupLogDto> Logs { get; set; } = new();
}

public class BackupLogDto
{
    public int Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}