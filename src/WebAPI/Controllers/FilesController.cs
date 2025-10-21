using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;

namespace SuperPanel.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Get directory contents
    /// </summary>
    [HttpGet("browse")]
    public async Task<ActionResult<List<FileSystemItem>>> BrowseDirectory([FromQuery] string path = "/")
    {
        var items = await _fileService.GetDirectoryContentsAsync(path);
        return Ok(items);
    }

    /// <summary>
    /// Get file information
    /// </summary>
    [HttpGet("info")]
    public async Task<ActionResult<FileSystemItem>> GetFileInfo([FromQuery] string path)
    {
        var fileInfo = await _fileService.GetFileInfoAsync(path);
        if (fileInfo == null)
            return NotFound();

        return Ok(fileInfo);
    }

    /// <summary>
    /// Read file content
    /// </summary>
    [HttpGet("content")]
    public async Task<ActionResult<string>> ReadFile([FromQuery] string filePath)
    {
        var content = await _fileService.ReadFileAsync(filePath);
        return Ok(new { content });
    }

    /// <summary>
    /// Write file content
    /// </summary>
    [HttpPost("content")]
    public async Task<IActionResult> WriteFile([FromBody] WriteFileRequest request)
    {
        var result = await _fileService.WriteFileAsync(request.FilePath, request.Content);
        if (!result)
            return BadRequest("Failed to write file");

        return Ok();
    }

    /// <summary>
    /// Delete file
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteFile([FromQuery] string filePath)
    {
        var result = await _fileService.DeleteFileAsync(filePath);
        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Create directory
    /// </summary>
    [HttpPost("directory")]
    public async Task<IActionResult> CreateDirectory([FromBody] CreateDirectoryRequest request)
    {
        var result = await _fileService.CreateDirectoryAsync(request.Path);
        if (!result)
            return BadRequest("Failed to create directory");

        return Ok();
    }

    /// <summary>
    /// Delete directory
    /// </summary>
    [HttpDelete("directory")]
    public async Task<IActionResult> DeleteDirectory([FromQuery] string path)
    {
        var result = await _fileService.DeleteDirectoryAsync(path);
        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Move file or directory
    /// </summary>
    [HttpPost("move")]
    public async Task<IActionResult> MoveFile([FromBody] MoveFileRequest request)
    {
        var result = await _fileService.MoveFileAsync(request.SourcePath, request.DestinationPath);
        if (!result)
            return BadRequest("Failed to move file");

        return Ok();
    }

    /// <summary>
    /// Copy file
    /// </summary>
    [HttpPost("copy")]
    public async Task<IActionResult> CopyFile([FromBody] CopyFileRequest request)
    {
        var result = await _fileService.CopyFileAsync(request.SourcePath, request.DestinationPath);
        if (!result)
            return BadRequest("Failed to copy file");

        return Ok();
    }
}

public class WriteFileRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class CreateDirectoryRequest
{
    public string Path { get; set; } = string.Empty;
}

public class MoveFileRequest
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}

public class CopyFileRequest
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}