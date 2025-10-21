using Microsoft.AspNetCore.Mvc;

namespace SuperPanel.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }

    /// <summary>
    /// Get basic system information without complex calculations
    /// </summary>
    [HttpGet("system")]
    public IActionResult GetSystemInfo()
    {
        return Ok(new
        {
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            WorkingSet = GC.GetTotalMemory(false) / (1024 * 1024), // MB
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get mock server data for testing
    /// </summary>
    [HttpGet("mock-servers")]
    public IActionResult GetMockServers()
    {
        var servers = new[]
        {
            new
            {
                Id = 1,
                Name = "Web Server 01",
                IpAddress = "192.168.1.10",
                Port = 80,
                Status = "Running",
                OperatingSystem = "Ubuntu 22.04 LTS",
                TotalMemoryMB = 8192,
                AvailableMemoryMB = 5400,
                CpuUsagePercent = 25.3,
                DiskUsagePercent = 42.1,
                CreatedAt = "2024-01-15T10:30:00Z",
                LastUpdated = DateTime.UtcNow
            },
            new
            {
                Id = 2,
                Name = "Database Server",
                IpAddress = "192.168.1.20",
                Port = 3306,
                Status = "Running",
                OperatingSystem = "CentOS Stream 9",
                TotalMemoryMB = 16384,
                AvailableMemoryMB = 12800,
                CpuUsagePercent = 15.7,
                DiskUsagePercent = 67.8,
                CreatedAt = "2024-01-20T14:20:00Z",
                LastUpdated = DateTime.UtcNow
            },
            new
            {
                Id = 3,
                Name = "Backup Server",
                IpAddress = "192.168.1.30",
                Port = 22,
                Status = "Stopped",
                OperatingSystem = "Debian 12",
                TotalMemoryMB = 4096,
                AvailableMemoryMB = 3200,
                CpuUsagePercent = 5.2,
                DiskUsagePercent = 89.3,
                CreatedAt = "2024-02-01T09:15:00Z",
                LastUpdated = DateTime.UtcNow
            }
        };

        return Ok(servers);
    }

    /// <summary>
    /// Get mock domain data for testing
    /// </summary>
    [HttpGet("mock-domains")]
    public IActionResult GetMockDomains()
    {
        var domains = new[]
        {
            new
            {
                Id = 1,
                DomainName = "example.com",
                ServerId = 1,
                ServerName = "Web Server 01",
                DocumentRoot = "/var/www/example.com",
                IsActive = true,
                SslEnabled = true,
                CreatedAt = "2024-01-15T11:00:00Z",
                LastUpdated = DateTime.UtcNow
            },
            new
            {
                Id = 2,
                DomainName = "test.org",
                ServerId = 1,
                ServerName = "Web Server 01",
                DocumentRoot = "/var/www/test.org",
                IsActive = true,
                SslEnabled = false,
                CreatedAt = "2024-01-18T09:30:00Z",
                LastUpdated = DateTime.UtcNow
            },
            new
            {
                Id = 3,
                DomainName = "demo.net",
                ServerId = 2,
                ServerName = "Database Server",
                DocumentRoot = "/var/www/demo.net",
                IsActive = false,
                SslEnabled = true,
                CreatedAt = "2024-02-01T14:45:00Z",
                LastUpdated = DateTime.UtcNow
            }
        };

        return Ok(domains);
    }
}