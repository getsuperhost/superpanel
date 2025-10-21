using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

public class DatabaseServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseService _databaseService;
    private readonly string _databaseName;

    public DatabaseServiceTests()
    {
        _databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        _context = new ApplicationDbContext(options);
        _databaseService = new DatabaseService(_context);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var server1 = new Server
        {
            Id = 1,
            Name = "Production Server",
            IpAddress = "192.168.1.100",
            Description = "Main production server",
            OperatingSystem = "Ubuntu 22.04",
            Status = ServerStatus.Online,
            UserId = 1,
            CreatedAt = DateTime.UtcNow
        };

        var server2 = new Server
        {
            Id = 2,
            Name = "Development Server",
            IpAddress = "192.168.1.101",
            Description = "Development environment",
            OperatingSystem = "Windows Server 2022",
            Status = ServerStatus.Online,
            UserId = 1,
            CreatedAt = DateTime.UtcNow
        };

        var databases = new List<Database>
        {
            new Database
            {
                Id = 1,
                Name = "ProductionDB",
                ServerId = 1,
                Type = "MySQL",
                Username = "produser",
                SizeInMB = 1500.0,
                Status = DatabaseStatus.Active,
                UserId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                BackupDate = DateTime.UtcNow.AddDays(-1)
            },
            new Database
            {
                Id = 2,
                Name = "TestDB",
                ServerId = 1,
                Type = "PostgreSQL",
                Username = "testuser",
                SizeInMB = 250.0,
                Status = DatabaseStatus.Active,
                UserId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            },
            new Database
            {
                Id = 3,
                Name = "DevDB",
                ServerId = 2,
                Type = "MySQL",
                Username = "devuser",
                SizeInMB = 100.0,
                Status = DatabaseStatus.Active,
                UserId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            }
        };

        _context.Servers.AddRange(server1, server2);
        _context.Databases.AddRange(databases);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetAllDatabasesAsync_ShouldReturnAllDatabases()
    {
        // Act
        var result = await _databaseService.GetAllDatabasesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(db => db.Name == "ProductionDB");
        result.Should().Contain(db => db.Name == "TestDB");
        result.Should().Contain(db => db.Name == "DevDB");
    }

    [Fact]
    public async Task GetDatabaseByIdAsync_WithValidId_ShouldReturnDatabase()
    {
        // Act
        var result = await _databaseService.GetDatabaseByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("ProductionDB");
        result.Type.Should().Be("MySQL");
        result.Username.Should().Be("produser");
        result.SizeInMB.Should().Be(1500.0);
        result.Status.Should().Be(DatabaseStatus.Active);
    }

    [Fact]
    public async Task GetDatabaseByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _databaseService.GetDatabaseByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateDatabaseAsync_ShouldCreateAndReturnDatabase()
    {
        // Arrange
        var newDatabase = new Database
        {
            Name = "NewDB",
            ServerId = 1,
            Type = "MongoDB",
            Username = "newuser",
            SizeInMB = 50.0,
            Status = DatabaseStatus.Active,
            UserId = 1
        };

        // Act
        var result = await _databaseService.CreateDatabaseAsync(newDatabase);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(0);
        result.Name.Should().Be("NewDB");
        result.Type.Should().Be("MongoDB");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Verify it was saved to database
        var savedDatabase = await _context.Databases.FindAsync(result.Id);
        savedDatabase.Should().NotBeNull();
        savedDatabase!.Name.Should().Be("NewDB");
    }

    [Fact]
    public async Task UpdateDatabaseAsync_WithValidId_ShouldUpdateAndReturnDatabase()
    {
        // Arrange
        var updateData = new Database
        {
            Name = "UpdatedProductionDB",
            Type = "MySQL 8.0",
            Username = "newproduser",
            SizeInMB = 2000.0,
            Status = DatabaseStatus.Suspended,
            BackupDate = DateTime.UtcNow
        };

        // Act
        var result = await _databaseService.UpdateDatabaseAsync(1, updateData);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("UpdatedProductionDB");
        result.Type.Should().Be("MySQL 8.0");
        result.Username.Should().Be("newproduser");
        result.SizeInMB.Should().Be(2000.0);
        result.Status.Should().Be(DatabaseStatus.Suspended);
        result.BackupDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Verify changes were persisted
        var savedDatabase = await _context.Databases.FindAsync(1);
        savedDatabase!.Name.Should().Be("UpdatedProductionDB");
    }

    [Fact]
    public async Task UpdateDatabaseAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var updateData = new Database
        {
            Name = "UpdatedDB",
            Type = "PostgreSQL",
            Username = "user",
            SizeInMB = 100.0,
            Status = DatabaseStatus.Active
        };

        // Act
        var result = await _databaseService.UpdateDatabaseAsync(999, updateData);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteDatabaseAsync_WithValidId_ShouldReturnTrue()
    {
        // Act
        var result = await _databaseService.DeleteDatabaseAsync(3);

        // Assert
        result.Should().BeTrue();

        // Verify database was deleted
        var deletedDatabase = await _context.Databases.FindAsync(3);
        deletedDatabase.Should().BeNull();
    }

    [Fact]
    public async Task DeleteDatabaseAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _databaseService.DeleteDatabaseAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetDatabasesByServerIdAsync_ShouldReturnServerDatabases()
    {
        // Act
        var result = await _databaseService.GetDatabasesByServerIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(db => db.Name == "ProductionDB");
        result.Should().Contain(db => db.Name == "TestDB");
        result.Should().NotContain(db => db.Name == "DevDB");
    }

    [Fact]
    public async Task GetDatabasesByServerIdAsync_WithNoMatches_ShouldReturnEmptyList()
    {
        // Act
        var result = await _databaseService.GetDatabasesByServerIdAsync(999);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllDatabasesAsync_ShouldIncludeServerRelation()
    {
        // Act
        var result = await _databaseService.GetAllDatabasesAsync();

        // Assert
        var database = result.FirstOrDefault(db => db.Id == 1);
        database.Should().NotBeNull();
        database!.Server.Should().NotBeNull();
        database.Server!.Name.Should().Be("Production Server");
    }

    [Fact]
    public async Task GetDatabaseByIdAsync_ShouldIncludeServerRelation()
    {
        // Act
        var result = await _databaseService.GetDatabaseByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Server.Should().NotBeNull();
        result.Server!.Name.Should().Be("Production Server");
    }

    [Fact]
    public async Task CreateDatabaseAsync_ShouldSetCreatedAtToUtcNow()
    {
        // Arrange
        var newDatabase = new Database
        {
            Name = "TimestampTestDB",
            ServerId = 1,
            Type = "MySQL",
            Username = "user",
            SizeInMB = 100.0,
            Status = DatabaseStatus.Active,
            UserId = 1
        };

        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _databaseService.CreateDatabaseAsync(newDatabase);

        var afterCreate = DateTime.UtcNow;

        // Assert
        result.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        result.CreatedAt.Should().BeOnOrBefore(afterCreate);
    }

    [Fact]
    public async Task UpdateDatabaseAsync_ShouldNotChangeCreatedAt()
    {
        // Arrange
        var originalDatabase = await _context.Databases.FindAsync(1);
        var originalCreatedAt = originalDatabase!.CreatedAt;

        var updateData = new Database
        {
            Name = "UpdatedName",
            Type = "MySQL",
            Username = "newuser",
            SizeInMB = 2000.0,
            Status = DatabaseStatus.Active
        };

        // Act
        await _databaseService.UpdateDatabaseAsync(1, updateData);

        // Assert
        var updatedDatabase = await _context.Databases.FindAsync(1);
        updatedDatabase!.CreatedAt.Should().Be(originalCreatedAt);
    }

    [Fact]
    public async Task CreateDatabaseAsync_MultipleDatabases_ShouldAssignUniqueIds()
    {
        // Arrange & Act
        var db1 = await _databaseService.CreateDatabaseAsync(new Database
        {
            Name = "DB1",
            ServerId = 1,
            Type = "MySQL",
            Username = "user1",
            Status = DatabaseStatus.Active,
            UserId = 1
        });

        var db2 = await _databaseService.CreateDatabaseAsync(new Database
        {
            Name = "DB2",
            ServerId = 1,
            Type = "PostgreSQL",
            Username = "user2",
            Status = DatabaseStatus.Active,
            UserId = 1
        });

        var db3 = await _databaseService.CreateDatabaseAsync(new Database
        {
            Name = "DB3",
            ServerId = 2,
            Type = "MongoDB",
            Username = "user3",
            Status = DatabaseStatus.Active,
            UserId = 1
        });

        // Assert
        db1.Id.Should().NotBe(db2.Id);
        db2.Id.Should().NotBe(db3.Id);
        db1.Id.Should().NotBe(db3.Id);
    }

    [Fact]
    public async Task GetDatabasesByServerIdAsync_ShouldIncludeUsersRelation()
    {
        // Act
        var result = await _databaseService.GetDatabasesByServerIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Should().AllSatisfy(db => db.Users.Should().NotBeNull());
    }

    [Theory]
    [InlineData(DatabaseStatus.Active)]
    [InlineData(DatabaseStatus.Inactive)]
    [InlineData(DatabaseStatus.Suspended)]
    public async Task CreateDatabaseAsync_WithDifferentStatuses_ShouldPreserveStatus(DatabaseStatus status)
    {
        // Arrange
        var newDatabase = new Database
        {
            Name = $"DB_{status}",
            ServerId = 1,
            Type = "MySQL",
            Username = "user",
            Status = status,
            UserId = 1
        };

        // Act
        var result = await _databaseService.CreateDatabaseAsync(newDatabase);

        // Assert
        result.Status.Should().Be(status);
    }

    [Fact]
    public async Task UpdateDatabaseAsync_ShouldUpdateBackupDate()
    {
        // Arrange
        var newBackupDate = DateTime.UtcNow;
        var updateData = new Database
        {
            Name = "ProductionDB",
            Type = "MySQL",
            Username = "produser",
            SizeInMB = 1500.0,
            Status = DatabaseStatus.Active,
            BackupDate = newBackupDate
        };

        // Act
        var result = await _databaseService.UpdateDatabaseAsync(1, updateData);

        // Assert
        result.Should().NotBeNull();
        result!.BackupDate.Should().BeCloseTo(newBackupDate, TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
