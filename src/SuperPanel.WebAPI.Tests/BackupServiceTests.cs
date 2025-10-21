using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<BackupService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IDbContextFactory<ApplicationDbContext>> _contextFactoryMock;
    private readonly BackupService _backupService;
    private readonly string _databaseName;

    public BackupServiceTests()
    {
        _databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<BackupService>>();
        _configurationMock = new Mock<IConfiguration>();
        _contextFactoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();

        // Setup the factory mock to return new context instances with the same options
        _contextFactoryMock.Setup(f => f.CreateDbContext()).Returns(() => new ApplicationDbContext(options));

        // Setup configuration mock
        _configurationMock.Setup(c => c["BackupSettings:BackupDirectory"])
            .Returns("/tmp/backups");

        _backupService = new BackupService(_contextFactoryMock.Object, _loggerMock.Object, _configurationMock.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var server = new Server
        {
            Id = 1,
            Name = "Test Server",
            IpAddress = "192.168.1.100",
            Description = "Test server",
            OperatingSystem = "Ubuntu 22.04",
            Status = ServerStatus.Online,
            CpuUsage = 50.0,
            MemoryUsage = 60.0,
            DiskUsage = 70.0,
            UserId = 1,
            CreatedAt = DateTime.UtcNow
        };

        var database = new Database
        {
            Id = 1,
            Name = "TestDB",
            ServerId = 1,
            Type = "MySQL",
            Username = "root",
            SizeInMB = 100.0,
            UserId = 1,
            Status = DatabaseStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@test.com",
            PasswordHash = "hash",
            Role = "Administrator",
            CreatedAt = DateTime.UtcNow
        };

        _context.Servers.Add(server);
        _context.Databases.Add(database);
        _context.Users.Add(user);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetBackupsAsync_ShouldReturnAllBackups()
    {
        // Act
        var result = await _backupService.GetBackupsAsync();

        // Assert
        result.Should().BeEmpty(); // No backups in test data initially
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldCreateAndReturnBackup()
    {
        // Arrange
        var request = new BackupRequest
        {
            Name = "Test Backup",
            Description = "Test backup description",
            Type = BackupType.Database,
            ServerId = 1,
            DatabaseId = 1,
            IsCompressed = true,
            IsEncrypted = false,
            RetentionDays = 30
        };

        // Act
        var result = await _backupService.CreateBackupAsync(request, 1);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(0);
        result.Name.Should().Be("Test Backup");
        result.Type.Should().Be(BackupType.Database);
        result.ServerId.Should().Be(1);
        result.DatabaseId.Should().Be(1);
        result.Status.Should().Be(BackupStatus.Pending); // Status is Pending when backup is created, changes to Running when execution starts
        result.CreatedByUserId.Should().Be(1);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetBackupAsync_WithValidId_ShouldReturnBackup()
    {
        // Arrange
        var request = new BackupRequest
        {
            Name = "Test Backup",
            Type = BackupType.Database,
            ServerId = 1,
            DatabaseId = 1,
            RetentionDays = 30
        };

        var createdBackup = await _backupService.CreateBackupAsync(request, 1);

        // Wait for async execution to complete
        await Task.Delay(100);

        // Act
        var result = await _backupService.GetBackupAsync(createdBackup.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(createdBackup.Id);
        result.Name.Should().Be("Test Backup");
    }

    [Fact]
    public async Task GetBackupAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _backupService.GetBackupAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBackupsAsync_WithServerId_ShouldFilterByServer()
    {
        // Arrange
        var request1 = new BackupRequest
        {
            Name = "Server 1 Backup",
            Type = BackupType.Database,
            ServerId = 1,
            RetentionDays = 30
        };

        var request2 = new BackupRequest
        {
            Name = "Server 2 Backup",
            Type = BackupType.Database,
            ServerId = null, // No server
            RetentionDays = 30
        };

        await _backupService.CreateBackupAsync(request1, 1);
        await _backupService.CreateBackupAsync(request2, 1);

        // Wait for async executions to complete
        await Task.Delay(200);

        // Act
        var result = await _backupService.GetBackupsAsync(serverId: 1);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Server 1 Backup");
        result.First().ServerId.Should().Be(1);
    }

    [Fact]
    public async Task GetBackupsAsync_WithDatabaseId_ShouldFilterByDatabase()
    {
        // Arrange
        var request1 = new BackupRequest
        {
            Name = "DB 1 Backup",
            Type = BackupType.Database,
            DatabaseId = 1,
            RetentionDays = 30
        };

        var request2 = new BackupRequest
        {
            Name = "DB 2 Backup",
            Type = BackupType.Database,
            DatabaseId = null, // No database
            RetentionDays = 30
        };

        await _backupService.CreateBackupAsync(request1, 1);
        await _backupService.CreateBackupAsync(request2, 1);

        // Wait for async executions to complete
        await Task.Delay(200);

        // Act
        var result = await _backupService.GetBackupsAsync(databaseId: 1);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("DB 1 Backup");
        result.First().DatabaseId.Should().Be(1);
    }

    [Fact]
    public async Task DeleteBackupAsync_WithValidId_ShouldReturnTrue()
    {
        // Arrange
        var request = new BackupRequest
        {
            Name = "Test Backup",
            Type = BackupType.Database,
            RetentionDays = 30
        };

        var createdBackup = await _backupService.CreateBackupAsync(request, 1);

        // Wait for async execution to complete (it will fail quickly since DatabaseId is null)
        await Task.Delay(100); // Give it time to fail

        // Verify backup exists before deletion
        var backupBeforeDelete = await _context.Backups.FindAsync(createdBackup.Id);
        backupBeforeDelete.Should().NotBeNull();

        // Act
        var result = await _backupService.DeleteBackupAsync(createdBackup.Id);

        // Assert
        result.Should().BeTrue();

        // Verify it was deleted from database using a fresh context
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;
        using (var verifyContext = new ApplicationDbContext(options))
        {
            var deletedBackup = await verifyContext.Backups.FindAsync(createdBackup.Id);
            deletedBackup.Should().BeNull();
        }
    }

    [Fact]
    public async Task DeleteBackupAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _backupService.DeleteBackupAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}