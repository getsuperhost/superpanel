using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

public class ServerServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ISystemMonitoringService> _systemMonitoringMock;
    private readonly ServerService _serverService;
    private readonly string _databaseName;

    public ServerServiceTests()
    {
        _databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        _context = new ApplicationDbContext(options);
        _systemMonitoringMock = new Mock<ISystemMonitoringService>();

        // Setup default mock responses
        _systemMonitoringMock.Setup(s => s.GetCpuUsageAsync())
            .ReturnsAsync(50.0);
        _systemMonitoringMock.Setup(s => s.GetAvailableMemoryAsync())
            .ReturnsAsync(60L);
        _systemMonitoringMock.Setup(s => s.GetDriveInfoAsync())
            .ReturnsAsync(new List<SuperPanel.WebAPI.Models.DriveInfo>
            {
                new SuperPanel.WebAPI.Models.DriveInfo { Name = "C:", UsagePercent = 70.0, TotalSizeGB = 1000, AvailableSpaceGB = 300 }
            });

        _serverService = new ServerService(_context, _systemMonitoringMock.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var servers = new List<Server>
        {
            new Server
            {
                Id = 1,
                Name = "Production Server",
                IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Description = "Main production server",
                OperatingSystem = "Ubuntu 22.04",
                Status = ServerStatus.Online,
                CpuUsage = 45.0,
                MemoryUsage = 60.0,
                DiskUsage = 50.0,
                UserId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new Server
            {
                Id = 2,
                Name = "Development Server",
                IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Description = "Development environment",
                OperatingSystem = "Windows Server 2022",
                Status = ServerStatus.Online,
                CpuUsage = 30.0,
                MemoryUsage = 40.0,
                DiskUsage = 35.0,
                UserId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            },
            new Server
            {
                Id = 3,
                Name = "Offline Server",
                IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Description = "Maintenance server",
                OperatingSystem = "Ubuntu 20.04",
                Status = ServerStatus.Offline,
                CpuUsage = 0.0,
                MemoryUsage = 0.0,
                DiskUsage = 0.0,
                UserId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            }
        };

        _context.Servers.AddRange(servers);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetAllServersAsync_ShouldReturnAllServers()
    {
        // Act
        var result = await _serverService.GetAllServersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(s => s.Name == "Production Server");
        result.Should().Contain(s => s.Name == "Development Server");
        result.Should().Contain(s => s.Name == "Offline Server");
    }

    [Fact]
    public async Task GetServerByIdAsync_WithValidId_ShouldReturnServer()
    {
        // Act
        var result = await _serverService.GetServerByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Production Server");
    result.IpAddress.Should().NotBeNullOrEmpty();
        result.Status.Should().Be(ServerStatus.Online);
    }

    [Fact]
    public async Task GetServerByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _serverService.GetServerByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateServerAsync_ShouldCreateAndReturnServer()
    {
        // Arrange
        var newServer = new Server
        {
            Name = "New Test Server",
            IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
            Description = "Newly created test server",
            OperatingSystem = "CentOS 8",
            Status = ServerStatus.Online,
            UserId = 1
        };

        // Act
        var result = await _serverService.CreateServerAsync(newServer);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(0);
        result.Name.Should().Be("New Test Server");
    result.IpAddress.Should().NotBeNullOrEmpty();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Verify it was saved to database
        var savedServer = await _context.Servers.FindAsync(result.Id);
        savedServer.Should().NotBeNull();
        savedServer!.Name.Should().Be("New Test Server");
    }

    [Fact]
    public async Task UpdateServerAsync_WithValidId_ShouldUpdateAndReturnServer()
    {
        // Arrange
        var updateData = new Server
        {
            Name = "Updated Production Server",
            IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
            Description = "Updated description",
            OperatingSystem = "Ubuntu 24.04",
            Status = ServerStatus.Maintenance
        };

        // Act
        var result = await _serverService.UpdateServerAsync(1, updateData);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Updated Production Server");
    result.IpAddress.Should().NotBeNullOrEmpty();
        result.Description.Should().Be("Updated description");
        result.OperatingSystem.Should().Be("Ubuntu 24.04");
        result.Status.Should().Be(ServerStatus.Maintenance);

        // Verify changes were persisted
        var savedServer = await _context.Servers.FindAsync(1);
        savedServer!.Name.Should().Be("Updated Production Server");
    }

    [Fact]
    public async Task UpdateServerAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var updateData = new Server
        {
            Name = "Updated Server",
            IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
            Description = "Updated description",
            OperatingSystem = "Ubuntu 24.04",
            Status = ServerStatus.Maintenance
        };

        // Act
        var result = await _serverService.UpdateServerAsync(999, updateData);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteServerAsync_WithValidId_ShouldReturnTrue()
    {
        // Act
        var result = await _serverService.DeleteServerAsync(3);

        // Assert
        result.Should().BeTrue();

        // Verify server was deleted
        var deletedServer = await _context.Servers.FindAsync(3);
        deletedServer.Should().BeNull();
    }

    [Fact]
    public async Task DeleteServerAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _serverService.DeleteServerAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateServerStatusAsync_WithValidId_ShouldUpdateStatus()
    {
        // Act
        var result = await _serverService.UpdateServerStatusAsync(3, ServerStatus.Online);

        // Assert
        result.Should().BeTrue();

        // Verify status was updated
        var server = await _context.Servers.FindAsync(3);
        server.Should().NotBeNull();
        server!.Status.Should().Be(ServerStatus.Online);
        server.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateServerStatusAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _serverService.UpdateServerStatusAsync(999, ServerStatus.Online);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateServerStatusAsync_ToOnline_ShouldUpdateSystemMetrics()
    {
        // Arrange
        var serverId = 3;

        // Act
        var result = await _serverService.UpdateServerStatusAsync(serverId, ServerStatus.Online);

        // Assert
        result.Should().BeTrue();

        // Verify monitoring service was called
        _systemMonitoringMock.Verify(s => s.GetCpuUsageAsync(), Times.Once);
        _systemMonitoringMock.Verify(s => s.GetAvailableMemoryAsync(), Times.Once);
        _systemMonitoringMock.Verify(s => s.GetDriveInfoAsync(), Times.Once);

        // Verify metrics were updated
        var server = await _context.Servers.FindAsync(serverId);
        server.Should().NotBeNull();
        server!.CpuUsage.Should().Be(50.0);
        server.MemoryUsage.Should().Be(60.0);
        server.DiskUsage.Should().Be(70.0);
    }

    [Fact]
    public async Task UpdateServerStatusAsync_ToOffline_ShouldNotUpdateMetrics()
    {
        // Arrange
        var serverId = 1;
        var originalCpuUsage = (await _context.Servers.FindAsync(serverId))!.CpuUsage;

        // Act
        var result = await _serverService.UpdateServerStatusAsync(serverId, ServerStatus.Offline);

        // Assert
        result.Should().BeTrue();

        // Verify monitoring service was NOT called
        _systemMonitoringMock.Verify(s => s.GetCpuUsageAsync(), Times.Never);

        // Verify metrics were NOT updated
        var server = await _context.Servers.FindAsync(serverId);
        server.Should().NotBeNull();
        server!.CpuUsage.Should().Be(originalCpuUsage);
    }

    [Fact]
    public async Task UpdateServerStatusAsync_WhenMonitoringFails_ShouldStillUpdateStatus()
    {
        // Arrange
        _systemMonitoringMock.Setup(s => s.GetCpuUsageAsync())
            .ThrowsAsync(new Exception("Monitoring service unavailable"));

        // Act
        var result = await _serverService.UpdateServerStatusAsync(3, ServerStatus.Online);

        // Assert
        result.Should().BeTrue();

        // Verify status was still updated despite monitoring failure
        var server = await _context.Servers.FindAsync(3);
        server.Should().NotBeNull();
        server!.Status.Should().Be(ServerStatus.Online);
    }

    [Fact]
    public async Task GetAllServersAsync_ShouldIncludeRelatedEntities()
    {
        // Arrange - Add related entities
        var domain = new Domain
        {
            Name = "test.com",
            ServerId = 1,
            Status = DomainStatus.Active,
            UserId = 1,
            CreatedAt = DateTime.UtcNow
        };

        var database = new Database
        {
            Name = "testdb",
            ServerId = 1,
            Type = "MySQL",
            Username = "root",
            Status = DatabaseStatus.Active,
            UserId = 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.Domains.Add(domain);
        _context.Databases.Add(database);
        await _context.SaveChangesAsync();

        // Act
        var result = await _serverService.GetAllServersAsync();

        // Assert
        var server = result.FirstOrDefault(s => s.Id == 1);
        server.Should().NotBeNull();
        server!.Domains.Should().NotBeNull();
        server.Domains.Should().HaveCount(1);
        server.Databases.Should().NotBeNull();
        server.Databases.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateServerAsync_ShouldSetCreatedAtToUtcNow()
    {
        // Arrange
        var newServer = new Server
        {
            Name = "Timestamp Test Server",
            IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
            Description = "Testing timestamp",
            OperatingSystem = "Ubuntu 22.04",
            Status = ServerStatus.Online,
            UserId = 1
        };

        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _serverService.CreateServerAsync(newServer);

        var afterCreate = DateTime.UtcNow;

        // Assert
        result.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        result.CreatedAt.Should().BeOnOrBefore(afterCreate);
    }

    [Fact]
    public async Task UpdateServerAsync_ShouldNotChangeCreatedAt()
    {
        // Arrange
        var originalServer = await _context.Servers.FindAsync(1);
        var originalCreatedAt = originalServer!.CreatedAt;

        var updateData = new Server
        {
            Name = "Updated Name",
            IpAddress = "ip-" + Guid.NewGuid().ToString("N").Substring(0, 12),
            Description = "Updated description",
            OperatingSystem = "Ubuntu 24.04",
            Status = ServerStatus.Maintenance
        };

        // Act
        await _serverService.UpdateServerAsync(1, updateData);

        // Assert
        var updatedServer = await _context.Servers.FindAsync(1);
        updatedServer!.CreatedAt.Should().Be(originalCreatedAt);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
