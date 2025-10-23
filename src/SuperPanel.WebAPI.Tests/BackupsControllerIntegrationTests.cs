using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SuperPanel.WebAPI.Controllers;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Data;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;

namespace SuperPanel.WebAPI.Tests;

public class BackupsControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    // Per-test temp directory for artifacts so cleanup is simple and reliable
    private readonly string _testTempDir = Path.Combine(Path.GetTempPath(), "superpanel_test", Guid.NewGuid().ToString("N"));

    public BackupsControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();

        // Best-effort cleanup of the per-test temp directory
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Do not throw from Dispose
        }
    }

    // xUnit IAsyncLifetime runs once per test instance (xUnit creates a new test class instance per test).
    // Use this to reset the shared SQLite in-memory database to a clean state before each test.
    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Drop and recreate schema to ensure tests are isolated from one another.
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        // Reseed the test user (tests expect a user with Id = 1)
        if (!db.Users.Any(u => u.Id == 1))
        {
            db.Users.Add(new User
            {
                Id = 1,
                Username = "testuser",
                Email = "testuser@example.com",
                PasswordHash = "test-hash",
                PasswordSalt = "test-salt",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync()
    {
        // Nothing to dispose per-test; the factory manages the shared connection lifetime.
        return Task.CompletedTask;
    }

    private string GenerateJwtToken(int userId = 1, string username = "testuser", string role = "User")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretKey1234567890123456789012345678901234567890"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("userId", userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "SuperPanel",
            audience: "SuperPanelUsers",
            claims: claims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task AuthenticateClientAsync()
    {
        var token = GenerateJwtToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<int> CreateTestServerAsync()
    {
        var server = new Server
        {
            Name = "Test Server - " + Guid.NewGuid().ToString("N").Substring(0, 8),
            // Use a unique IpAddress string per call to avoid UNIQUE constraint collisions in parallel tests
            IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
            OperatingSystem = "Linux",
            Status = ServerStatus.Online
        };

        var response = await _client.PostAsJsonAsync("/api/servers", server);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdServer = await response.Content.ReadFromJsonAsync<Server>();
        createdServer.Should().NotBeNull();

        return createdServer!.Id;
    }

    private async Task<int> CreateTestDatabaseAsync(int serverId)
    {

        var database = new Database
        {
            Name = "Test Database - " + Guid.NewGuid().ToString("N").Substring(0, 8),
            Type = "MySQL",
            Username = "dbuser_" + Guid.NewGuid().ToString("N").Substring(0, 6),
            ServerId = serverId,
            Status = DatabaseStatus.Active
        };

        var response = await _client.PostAsJsonAsync("/api/databases", database);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdDatabase = await response.Content.ReadFromJsonAsync<Database>();
        createdDatabase.Should().NotBeNull();

        return createdDatabase!.Id;
    }

    private async Task<int> CreateTestDomainAsync(int serverId)
    {
        var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
        var domainRequest = new
        {
            name = $"testdomain-{unique}.example",
            serverId = serverId,
            documentRoot = $"/var/www/testdomain_{unique}",
            isActive = true
        };

        var response = await _client.PostAsJsonAsync("/api/domains", domainRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var domain = await response.Content.ReadFromJsonAsync<Domain>();
        domain.Should().NotBeNull();

        return domain!.Id;
    }

    [Fact]
    public async Task GetBackups_WithoutFilters_ShouldReturnAllBackups()
    {
        // Arrange
        await AuthenticateClientAsync();
        var serverId = await CreateTestServerAsync();
        var databaseId = await CreateTestDatabaseAsync(serverId);
        var domainId = await CreateTestDomainAsync(serverId);

                // Create multiple backups
        await CreateTestBackupAsync(serverId, databaseId, domainId, "Backup 1");
        await CreateTestBackupAsync(serverId, databaseId, domainId, "Backup 2");

        // Act
        var response = await _client.GetAsync("/api/backups");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var backups = await response.Content.ReadFromJsonAsync<List<BackupDto>>();
        backups.Should().NotBeNull();
        backups.Should().HaveCountGreaterThanOrEqualTo(2);
        backups.Should().Contain(b => b.Name == "Backup 1");
        backups.Should().Contain(b => b.Name == "Backup 2");
    }

    [Fact]
    public async Task GetBackups_WithServerFilter_ShouldReturnFilteredBackups()
    {
        // Arrange
        await AuthenticateClientAsync();
        var serverId = await CreateTestServerAsync();
        var databaseId = await CreateTestDatabaseAsync(serverId);
        var domainId = await CreateTestDomainAsync(serverId);

        await CreateTestBackupAsync(serverId, databaseId, domainId, "Server Backup");

        // Act
        var response = await _client.GetAsync($"/api/backups?serverId={serverId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var backups = await response.Content.ReadFromJsonAsync<List<BackupDto>>();
        backups.Should().NotBeNull();
        backups.Should().Contain(b => b.ServerId == serverId);
    }

    [Fact]
    public async Task GetBackup_WithValidId_ShouldReturnBackup()
    {
        // Arrange
        await AuthenticateClientAsync();
        var serverId = await CreateTestServerAsync();
        var databaseId = await CreateTestDatabaseAsync(serverId);
        var domainId = await CreateTestDomainAsync(serverId);

        var backupId = await CreateTestBackupAsync(serverId, databaseId, domainId, "Test Backup");

        // Act
        var response = await _client.GetAsync($"/api/backups/{backupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var backup = await response.Content.ReadFromJsonAsync<BackupDto>();
        backup.Should().NotBeNull();
        backup!.Id.Should().Be(backupId);
        backup.Name.Should().Be("Test Backup");
    }

    [Fact]
    public async Task RestoreBackup_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        await AuthenticateClientAsync();
        var restoreRequest = new
        {
            targetServerId = 1,
            targetDatabaseId = 1,
            targetPath = "/var/restore",
            overwriteExisting = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/backups/99999/restore", restoreRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateBackup_WithValidData_ShouldCreateBackup()
    {
        // Arrange
        await AuthenticateClientAsync();
        var serverId = await CreateTestServerAsync();
        var databaseId = await CreateTestDatabaseAsync(serverId);
        var domainId = await CreateTestDomainAsync(serverId);

        var createRequest = new
        {
            name = "New Backup",
            description = "Test backup creation",
            type = BackupType.Database,
            serverId = serverId,
            databaseId = databaseId,
            domainId = domainId,
            backupPath = "/var/backups",
            isCompressed = true,
            isEncrypted = false,
            retentionDays = 30
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/backups", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var backup = await response.Content.ReadFromJsonAsync<BackupDto>();
        backup.Should().NotBeNull();
        backup!.Name.Should().Be("New Backup");
        backup.Type.Should().Be(BackupType.Database);
        backup.Status.Should().Be(BackupStatus.Pending);
        backup.CreatedByUserId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateBackup_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        await AuthenticateClientAsync();
        var createRequest = new
        {
            name = "", // Invalid: empty name
            description = "Test backup",
            type = BackupType.Database
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/backups", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteBackup_WithValidId_ShouldDeleteBackup()
    {
        // Arrange
        await AuthenticateClientAsync();
        var serverId = await CreateTestServerAsync();
        var databaseId = await CreateTestDatabaseAsync(serverId);
        var domainId = await CreateTestDomainAsync(serverId);

        var backupId = await CreateTestBackupAsync(serverId, databaseId, domainId, "Backup to Delete");

        // Act
        var response = await _client.DeleteAsync($"/api/backups/{backupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify backup is deleted
        var getResponse = await _client.GetAsync($"/api/backups/{backupId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBackup_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        await AuthenticateClientAsync();

        // Act
        var response = await _client.DeleteAsync("/api/backups/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestoreBackup_WithValidData_ShouldRestoreSuccessfully()
    {
        // Arrange
        await AuthenticateClientAsync();
        var serverId = await CreateTestServerAsync();
        var databaseId = await CreateTestDatabaseAsync(serverId);
        var domainId = await CreateTestDomainAsync(serverId);

        var backupId = await CreateTestBackupAsync(serverId, databaseId, domainId, "Backup to Restore");
        await MarkBackupAsCompletedAsync(backupId);

        var restoreRequest = new
        {
            targetServerId = serverId,
            targetDatabaseId = databaseId,
            targetPath = "/var/restore",
            overwriteExisting = false
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/backups/{backupId}/restore", restoreRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RestoreResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.BytesRestored.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task DownloadBackup_WithCompletedBackup_ShouldReturnFile()
    {
        // Arrange
        await AuthenticateClientAsync();
        var serverId = await CreateTestServerAsync();
        var databaseId = await CreateTestDatabaseAsync(serverId);
        var domainId = await CreateTestDomainAsync(serverId);

        var backupId = await CreateTestBackupAsync(serverId, databaseId, domainId, "Backup to Download");

        // Mark backup as completed (simulate completion)
        await MarkBackupAsCompletedAsync(backupId);

        // Act
        var response = await _client.GetAsync($"/api/backups/{backupId}/download");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
        response.Content.Headers.ContentDisposition.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadBackup_WithPendingBackup_ShouldReturnBadRequest()
    {
        // Arrange
        await AuthenticateClientAsync();
        var serverId = await CreateTestServerAsync();
        var databaseId = await CreateTestDatabaseAsync(serverId);
        var domainId = await CreateTestDomainAsync(serverId);

        var backupId = await CreateTestBackupAsync(serverId, databaseId, domainId, "Pending Backup");

        // Ensure the backup remains in Pending state (background services might complete it)
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var backup = await context.Backups.FindAsync(backupId);
            if (backup != null)
            {
                backup.Status = BackupStatus.Pending;
                // Use empty string for FilePath to satisfy nullable reference checks in tests
                backup.FilePath = string.Empty;
                backup.FileSizeInBytes = 0;
                await context.SaveChangesAsync();
            }
        }

        // Act
        var response = await _client.GetAsync($"/api/backups/{backupId}/download");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<int> CreateTestBackupAsync(int serverId, int databaseId, int domainId, string name)
    {
        var createRequest = new
        {
            name = name,
            description = $"Test backup: {name}",
            type = BackupType.Database,
            serverId = serverId,
            databaseId = databaseId,
            domainId = domainId,
            backupPath = "/var/backups",
            isCompressed = true,
            isEncrypted = false,
            retentionDays = 30
        };

        var response = await _client.PostAsJsonAsync("/api/backups", createRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var backup = await response.Content.ReadFromJsonAsync<BackupDto>();
        backup.Should().NotBeNull();

        return backup!.Id;
    }

    private async Task MarkBackupAsCompletedAsync(int backupId)
    {
        // This would typically be done by the backup service, but for testing we need to simulate it
        // In a real scenario, this would be handled by the backup process completing
        // For now, we'll update the backup status to Completed in the database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var backup = await context.Backups.FindAsync(backupId);
        if (backup != null)
        {
            backup.Status = BackupStatus.Completed;
            // Set a dummy file path for testing download/restore
            // Create a per-test backups directory and place artifacts there
            string backupDir = Path.Combine(_testTempDir, "backups");
            Directory.CreateDirectory(backupDir);

            string dummyFilePath;
            if (backup.IsCompressed)
            {
                // Create a small directory with a file and compress it to a zip so restore can extract it
                string tempDir = Path.Combine(_testTempDir, "zip_src");
                Directory.CreateDirectory(tempDir);
                string innerFile = Path.Combine(tempDir, "content.txt");
                await File.WriteAllTextAsync(innerFile, "This is a dummy compressed backup file for testing.");

                dummyFilePath = Path.Combine(backupDir, $"test_backup_{backupId}.zip");
                if (File.Exists(dummyFilePath)) File.Delete(dummyFilePath);
                ZipFile.CreateFromDirectory(tempDir, dummyFilePath);

                // cleanup temp source dir
                try { Directory.Delete(tempDir, true); } catch { }
            }
            else
            {
                dummyFilePath = Path.Combine(backupDir, $"test_backup_{backupId}.bak");
                // Create a dummy file with some content
                await File.WriteAllTextAsync(dummyFilePath, "This is a dummy backup file for testing.");
            }

            backup.FilePath = dummyFilePath;
            backup.FileSizeInBytes = new FileInfo(dummyFilePath).Length;
            await context.SaveChangesAsync();
        }
    }
}

// Helper classes for deserialization
public class RestoreResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long BytesRestored { get; set; }
}