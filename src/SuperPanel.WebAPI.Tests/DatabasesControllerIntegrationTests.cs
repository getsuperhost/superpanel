using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperPanel.WebAPI.Controllers;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

/// <summary>
/// Integration tests for DatabasesController endpoints.
/// Tests the full HTTP pipeline including authentication, authorization, and database integration.
/// </summary>
public class DatabasesControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testDatabaseName;

    public DatabasesControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _testDatabaseName = $"TestDb_{Guid.NewGuid()}";
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add DbContext using in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_testDatabaseName);
                });

                // Build the service provider and ensure database is created
                var serviceProvider = services.BuildServiceProvider();
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    #region Helper Methods

    private async Task<(string token, int userId)> RegisterAndLoginUserAsync(string username, string email, string password, string role = "User")
    {
        var registerRequest = new RegisterRequest
        {
            Username = username,
            Email = email,
            Password = password,
            Role = role
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        
        return (authResponse!.Token, authResponse.User.Id);
    }

    private void SetAuthToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Server> CreateServerAsync(int userId)
    {
        var server = new Server
        {
            Name = "Test Server",
            IpAddress = "192.168.1.100",
            Status = ServerStatus.Online,
            UserId = userId
        };

        var response = await _client.PostAsJsonAsync("/api/servers", server);
        return (await response.Content.ReadFromJsonAsync<Server>())!;
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task GetDatabases_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/databases");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDatabase_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var database = new Database
        {
            Name = "TestDB",
            Type = "MySQL",
            Status = DatabaseStatus.Active
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/databases", database);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region CRUD Tests with Authentication

    [Fact]
    public async Task GetDatabases_WithAuthentication_ShouldReturnUserDatabases()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("dbuser", "db@example.com", "DbPass123!");
        SetAuthToken(token);

        var server = await CreateServerAsync(userId);

        // Create a database for this user
        var database = new Database
        {
            Name = "UserDatabase",
            Type = "PostgreSQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id,
            UserId = userId
        };
        await _client.PostAsJsonAsync("/api/databases", database);

        // Act
        var response = await _client.GetAsync("/api/databases");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var databases = await response.Content.ReadFromJsonAsync<List<Database>>();
        databases.Should().NotBeNull();
        databases.Should().HaveCountGreaterThan(0);
        databases!.Should().OnlyContain(d => d.UserId == userId);
    }

    [Fact]
    public async Task CreateDatabase_WithValidData_ShouldReturnCreatedDatabase()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("createdbuser", "createdb@example.com", "CreateDbPass123!");
        SetAuthToken(token);

        var server = await CreateServerAsync(userId);

        var database = new Database
        {
            Name = "NewDatabase",
            Type = "MySQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id,
            SizeInMB = 100.0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/databases", database);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdDatabase = await response.Content.ReadFromJsonAsync<Database>();
        createdDatabase.Should().NotBeNull();
        createdDatabase!.Name.Should().Be("NewDatabase");
        createdDatabase.Type.Should().Be("MySQL");
        createdDatabase.UserId.Should().Be(userId); // Should be assigned to current user
        createdDatabase.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetDatabaseById_WithOwnership_ShouldReturnDatabase()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("getdbuser", "getdb@example.com", "GetDbPass123!");
        SetAuthToken(token);

        var server = await CreateServerAsync(userId);

        // Create database
        var createResponse = await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "GetTestDB",
            Type = "MongoDB",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });
        var createdDatabase = await createResponse.Content.ReadFromJsonAsync<Database>();

        // Act
        var response = await _client.GetAsync($"/api/databases/{createdDatabase!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var database = await response.Content.ReadFromJsonAsync<Database>();
        database.Should().NotBeNull();
        database!.Id.Should().Be(createdDatabase.Id);
        database.Name.Should().Be("GetTestDB");
    }

    [Fact]
    public async Task UpdateDatabase_WithOwnership_ShouldReturnUpdatedDatabase()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("updatedbuser", "updatedb@example.com", "UpdateDbPass123!");
        SetAuthToken(token);

        var server = await CreateServerAsync(userId);

        // Create database
        var createResponse = await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "OriginalDB",
            Type = "MySQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });
        var createdDatabase = await createResponse.Content.ReadFromJsonAsync<Database>();

        // Update database
        var updatedData = new Database
        {
            Id = createdDatabase!.Id,
            Name = "UpdatedDB",
            Type = "PostgreSQL",
            Status = DatabaseStatus.Suspended,
            ServerId = server.Id,
            SizeInMB = 500.0,
            UserId = userId
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/databases/{createdDatabase.Id}", updatedData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updatedDatabase = await response.Content.ReadFromJsonAsync<Database>();
        updatedDatabase.Should().NotBeNull();
        updatedDatabase!.Name.Should().Be("UpdatedDB");
        updatedDatabase.Type.Should().Be("PostgreSQL");
        updatedDatabase.Status.Should().Be(DatabaseStatus.Suspended);
        updatedDatabase.SizeInMB.Should().Be(500.0);
    }

    [Fact]
    public async Task DeleteDatabase_WithOwnership_ShouldReturnNoContent()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("deletedbuser", "deletedb@example.com", "DeleteDbPass123!");
        SetAuthToken(token);

        var server = await CreateServerAsync(userId);

        // Create database
        var createResponse = await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "ToBeDeleted",
            Type = "Redis",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });
        var createdDatabase = await createResponse.Content.ReadFromJsonAsync<Database>();

        // Act
        var response = await _client.DeleteAsync($"/api/databases/{createdDatabase!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/databases/{createdDatabase.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Server Association Tests

    [Fact]
    public async Task GetDatabasesByServer_ShouldReturnDatabasesForServer()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("serverdbuser", "serverdb@example.com", "ServerDbPass123!");
        SetAuthToken(token);

        var server = await CreateServerAsync(userId);

        // Create multiple databases for the server
        await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "ServerDB1",
            Type = "MySQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });
        await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "ServerDB2",
            Type = "PostgreSQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });

        // Act
        var response = await _client.GetAsync($"/api/databases/server/{server.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var databases = await response.Content.ReadFromJsonAsync<List<Database>>();
        databases.Should().NotBeNull();
        databases.Should().HaveCount(2);
        databases!.Should().OnlyContain(d => d.ServerId == server.Id);
    }

    #endregion

    #region Authorization Tests (User Isolation)

    [Fact]
    public async Task GetDatabaseById_WithoutOwnership_ShouldReturnForbidden()
    {
        // Arrange - Create database with user1
        var (token1, userId1) = await RegisterAndLoginUserAsync("dbowner", "dbowner@example.com", "DbOwnerPass123!");
        SetAuthToken(token1);

        var server = await CreateServerAsync(userId1);

        var createResponse = await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "Owner's Database",
            Type = "MySQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });
        var createdDatabase = await createResponse.Content.ReadFromJsonAsync<Database>();

        // Switch to user2
        var (token2, userId2) = await RegisterAndLoginUserAsync("dbother", "dbother@example.com", "DbOtherPass123!");
        SetAuthToken(token2);

        // Act - Try to access user1's database as user2
        var response = await _client.GetAsync($"/api/databases/{createdDatabase!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateDatabase_WithoutOwnership_ShouldReturnForbidden()
    {
        // Arrange - Create database with user1
        var (token1, userId1) = await RegisterAndLoginUserAsync("dbowner2", "dbowner2@example.com", "DbOwner2Pass123!");
        SetAuthToken(token1);

        var server = await CreateServerAsync(userId1);

        var createResponse = await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "Owner's Database 2",
            Type = "PostgreSQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });
        var createdDatabase = await createResponse.Content.ReadFromJsonAsync<Database>();

        // Switch to user2
        var (token2, userId2) = await RegisterAndLoginUserAsync("dbother2", "dbother2@example.com", "DbOther2Pass123!");
        SetAuthToken(token2);

        // Act - Try to update user1's database as user2
        var updatedData = new Database
        {
            Id = createdDatabase!.Id,
            Name = "Hacked DB",
            Type = "MySQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id,
            UserId = userId1
        };
        var response = await _client.PutAsJsonAsync($"/api/databases/{createdDatabase.Id}", updatedData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteDatabase_WithoutOwnership_ShouldReturnForbidden()
    {
        // Arrange - Create database with user1
        var (token1, userId1) = await RegisterAndLoginUserAsync("dbowner3", "dbowner3@example.com", "DbOwner3Pass123!");
        SetAuthToken(token1);

        var server = await CreateServerAsync(userId1);

        var createResponse = await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "Owner's Database 3",
            Type = "MongoDB",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });
        var createdDatabase = await createResponse.Content.ReadFromJsonAsync<Database>();

        // Switch to user2
        var (token2, userId2) = await RegisterAndLoginUserAsync("dbother3", "dbother3@example.com", "DbOther3Pass123!");
        SetAuthToken(token2);

        // Act - Try to delete user1's database as user2
        var response = await _client.DeleteAsync($"/api/databases/{createdDatabase!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Administrator Tests

    [Fact]
    public async Task GetDatabases_AsAdministrator_ShouldReturnAllDatabases()
    {
        // Arrange - Create databases for two different users
        var (token1, userId1) = await RegisterAndLoginUserAsync("dbuser1", "dbuser1@example.com", "DbUser1Pass123!");
        SetAuthToken(token1);
        var server1 = await CreateServerAsync(userId1);
        await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "User1 Database",
            Type = "MySQL",
            Status = DatabaseStatus.Active,
            ServerId = server1.Id
        });

        var (token2, userId2) = await RegisterAndLoginUserAsync("dbuser2", "dbuser2@example.com", "DbUser2Pass123!");
        SetAuthToken(token2);
        var server2 = await CreateServerAsync(userId2);
        await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "User2 Database",
            Type = "PostgreSQL",
            Status = DatabaseStatus.Active,
            ServerId = server2.Id
        });

        // Create admin user
        var (adminToken, adminId) = await RegisterAndLoginUserAsync("dbadmin", "dbadmin@example.com", "DbAdminPass123!", "Administrator");
        SetAuthToken(adminToken);

        // Act
        var response = await _client.GetAsync("/api/databases");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var databases = await response.Content.ReadFromJsonAsync<List<Database>>();
        databases.Should().NotBeNull();
        databases.Should().HaveCountGreaterOrEqualTo(2); // Should see all users' databases
        databases!.Should().Contain(d => d.UserId == userId1);
        databases.Should().Contain(d => d.UserId == userId2);
    }

    [Fact]
    public async Task UpdateDatabase_AsAdministrator_ShouldSucceedForAnyDatabase()
    {
        // Arrange - Create database with regular user
        var (userToken, userId) = await RegisterAndLoginUserAsync("dbregular", "dbregular@example.com", "DbRegularPass123!");
        SetAuthToken(userToken);

        var server = await CreateServerAsync(userId);

        var createResponse = await _client.PostAsJsonAsync("/api/databases", new Database
        {
            Name = "User Database",
            Type = "MySQL",
            Status = DatabaseStatus.Active,
            ServerId = server.Id
        });
        var createdDatabase = await createResponse.Content.ReadFromJsonAsync<Database>();

        // Switch to admin
        var (adminToken, adminId) = await RegisterAndLoginUserAsync("dbadmin2", "dbadmin2@example.com", "DbAdmin2Pass123!", "Administrator");
        SetAuthToken(adminToken);

        // Act - Admin updates user's database
        var updatedData = new Database
        {
            Id = createdDatabase!.Id,
            Name = "Admin Updated DB",
            Type = "PostgreSQL",
            Status = DatabaseStatus.Suspended,
            ServerId = server.Id,
            UserId = userId
        };
        var response = await _client.PutAsJsonAsync($"/api/databases/{createdDatabase.Id}", updatedData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updatedDatabase = await response.Content.ReadFromJsonAsync<Database>();
        updatedDatabase.Should().NotBeNull();
        updatedDatabase!.Name.Should().Be("Admin Updated DB");
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task GetDatabaseById_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("dbnotfound", "dbnotfound@example.com", "DbNotFoundPass123!");
        SetAuthToken(token);

        // Act
        var response = await _client.GetAsync("/api/databases/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateDatabase_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("dbupdatenotfound", "dbupdatenotfound@example.com", "DbUpdateNotFoundPass123!");
        SetAuthToken(token);

        var updatedData = new Database
        {
            Id = 99999,
            Name = "Non-existent",
            Type = "MySQL",
            Status = DatabaseStatus.Active,
            UserId = userId
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/databases/99999", updatedData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteDatabase_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("dbdeletenotfound", "dbdeletenotfound@example.com", "DbDeleteNotFoundPass123!");
        SetAuthToken(token);

        // Act
        var response = await _client.DeleteAsync("/api/databases/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
