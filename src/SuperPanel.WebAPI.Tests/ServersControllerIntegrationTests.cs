using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperPanel.WebAPI.Controllers;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

/// <summary>
/// Test web application factory for ServersController integration tests
/// </summary>
public class ServersTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}

/// <summary>
/// Integration tests for ServersController endpoints.
/// Tests the full HTTP pipeline including authentication, authorization, and database integration.
/// </summary>
public class ServersControllerIntegrationTests : IClassFixture<ServersTestWebApplicationFactory>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testDatabaseName;

    public ServersControllerIntegrationTests(ServersTestWebApplicationFactory factory)
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

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task GetServers_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/servers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateServer_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var server = new Server
        {
            Name = "Test Server",
            IpAddress = "192.168.1.100",
            Status = ServerStatus.Offline
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/servers", server);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region CRUD Tests with Authentication

    [Fact]
    public async Task GetServers_WithAuthentication_ShouldReturnUserServers()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("serveruser", "server@example.com", "ServerPass123!");
        SetAuthToken(token);

        // Create a server for this user
        var server = new Server
        {
            Name = "User Server",
            IpAddress = "192.168.1.10",
            Status = ServerStatus.Online,
            UserId = userId
        };
        await _client.PostAsJsonAsync("/api/servers", server);

        // Act
        var response = await _client.GetAsync("/api/servers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var servers = await response.Content.ReadFromJsonAsync<List<Server>>();
        servers.Should().NotBeNull();
        servers.Should().HaveCountGreaterThan(0);
        servers!.Should().OnlyContain(s => s.UserId == userId);
    }

    [Fact]
    public async Task CreateServer_WithValidData_ShouldReturnCreatedServer()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("createuser", "create@example.com", "CreatePass123!");
        SetAuthToken(token);

        var server = new Server
        {
            Name = "New Server",
            IpAddress = "10.0.0.1",
            OperatingSystem = "Ubuntu 22.04",
            Status = ServerStatus.Offline
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/servers", server);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdServer = await response.Content.ReadFromJsonAsync<Server>();
        createdServer.Should().NotBeNull();
        createdServer!.Name.Should().Be("New Server");
        createdServer.IpAddress.Should().Be("10.0.0.1");
        createdServer.UserId.Should().Be(userId); // Should be assigned to current user
        createdServer.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetServerById_WithOwnership_ShouldReturnServer()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("getuser", "get@example.com", "GetPass123!");
        SetAuthToken(token);

        // Create server
        var createResponse = await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "Get Test Server",
            IpAddress = "172.16.0.1",
            Status = ServerStatus.Online
        });
        var createdServer = await createResponse.Content.ReadFromJsonAsync<Server>();

        // Act
        var response = await _client.GetAsync($"/api/servers/{createdServer!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var server = await response.Content.ReadFromJsonAsync<Server>();
        server.Should().NotBeNull();
        server!.Id.Should().Be(createdServer.Id);
        server.Name.Should().Be("Get Test Server");
    }

    [Fact]
    public async Task UpdateServer_WithOwnership_ShouldReturnUpdatedServer()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("updateuser", "update@example.com", "UpdatePass123!");
        SetAuthToken(token);

        // Create server
        var createResponse = await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "Original Name",
            IpAddress = "192.168.2.1",
            Status = ServerStatus.Offline
        });
        var createdServer = await createResponse.Content.ReadFromJsonAsync<Server>();

        // Update server
        var updatedData = new Server
        {
            Id = createdServer!.Id,
            Name = "Updated Name",
            IpAddress = "192.168.2.2",
            OperatingSystem = "CentOS 8",
            Status = ServerStatus.Online,
            UserId = userId
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/servers/{createdServer.Id}", updatedData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updatedServer = await response.Content.ReadFromJsonAsync<Server>();
        updatedServer.Should().NotBeNull();
        updatedServer!.Name.Should().Be("Updated Name");
        updatedServer.IpAddress.Should().Be("192.168.2.2");
        updatedServer.OperatingSystem.Should().Be("CentOS 8");
        updatedServer.Status.Should().Be(ServerStatus.Online);
    }

    [Fact]
    public async Task DeleteServer_WithOwnership_ShouldReturnNoContent()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("deleteuser", "delete@example.com", "DeletePass123!");
        SetAuthToken(token);

        // Create server
        var createResponse = await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "To Be Deleted",
            IpAddress = "192.168.3.1",
            Status = ServerStatus.Offline
        });
        var createdServer = await createResponse.Content.ReadFromJsonAsync<Server>();

        // Act
        var response = await _client.DeleteAsync($"/api/servers/{createdServer!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/servers/{createdServer.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Authorization Tests (User Isolation)

    [Fact]
    public async Task GetServerById_WithoutOwnership_ShouldReturnForbidden()
    {
        // Arrange - Create server with user1
        var (token1, userId1) = await RegisterAndLoginUserAsync("owner", "owner@example.com", "OwnerPass123!");
        SetAuthToken(token1);

        var createResponse = await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "Owner's Server",
            IpAddress = "192.168.4.1",
            Status = ServerStatus.Online
        });
        var createdServer = await createResponse.Content.ReadFromJsonAsync<Server>();

        // Switch to user2
        var (token2, userId2) = await RegisterAndLoginUserAsync("other", "other@example.com", "OtherPass123!");
        SetAuthToken(token2);

        // Act - Try to access user1's server as user2
        var response = await _client.GetAsync($"/api/servers/{createdServer!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateServer_WithoutOwnership_ShouldReturnForbidden()
    {
        // Arrange - Create server with user1
        var (token1, userId1) = await RegisterAndLoginUserAsync("owner2", "owner2@example.com", "Owner2Pass123!");
        SetAuthToken(token1);

        var createResponse = await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "Owner's Server 2",
            IpAddress = "192.168.5.1",
            Status = ServerStatus.Online
        });
        var createdServer = await createResponse.Content.ReadFromJsonAsync<Server>();

        // Switch to user2
        var (token2, userId2) = await RegisterAndLoginUserAsync("other2", "other2@example.com", "Other2Pass123!");
        SetAuthToken(token2);

        // Act - Try to update user1's server as user2
        var updatedData = new Server
        {
            Id = createdServer!.Id,
            Name = "Hacked Name",
            IpAddress = "192.168.5.2",
            Status = ServerStatus.Offline,
            UserId = userId1
        };
        var response = await _client.PutAsJsonAsync($"/api/servers/{createdServer.Id}", updatedData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteServer_WithoutOwnership_ShouldReturnForbidden()
    {
        // Arrange - Create server with user1
        var (token1, userId1) = await RegisterAndLoginUserAsync("owner3", "owner3@example.com", "Owner3Pass123!");
        SetAuthToken(token1);

        var createResponse = await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "Owner's Server 3",
            IpAddress = "192.168.6.1",
            Status = ServerStatus.Online
        });
        var createdServer = await createResponse.Content.ReadFromJsonAsync<Server>();

        // Switch to user2
        var (token2, userId2) = await RegisterAndLoginUserAsync("other3", "other3@example.com", "Other3Pass123!");
        SetAuthToken(token2);

        // Act - Try to delete user1's server as user2
        var response = await _client.DeleteAsync($"/api/servers/{createdServer!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Administrator Tests

    [Fact]
    public async Task GetServers_AsAdministrator_ShouldReturnAllServers()
    {
        // Arrange - Create servers for two different users
        var (token1, userId1) = await RegisterAndLoginUserAsync("user1", "user1@example.com", "User1Pass123!");
        SetAuthToken(token1);
        await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "User1 Server",
            IpAddress = "192.168.7.1",
            Status = ServerStatus.Online
        });

        var (token2, userId2) = await RegisterAndLoginUserAsync("user2", "user2@example.com", "User2Pass123!");
        SetAuthToken(token2);
        await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "User2 Server",
            IpAddress = "192.168.7.2",
            Status = ServerStatus.Online
        });

        // Create admin user
        var (adminToken, adminId) = await RegisterAndLoginUserAsync("admin", "admin@example.com", "AdminPass123!", "Administrator");
        SetAuthToken(adminToken);

        // Act
        var response = await _client.GetAsync("/api/servers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var servers = await response.Content.ReadFromJsonAsync<List<Server>>();
        servers.Should().NotBeNull();
        servers.Should().HaveCountGreaterOrEqualTo(2); // Should see all users' servers
        servers!.Should().Contain(s => s.UserId == userId1);
        servers.Should().Contain(s => s.UserId == userId2);
    }

    [Fact]
    public async Task UpdateServer_AsAdministrator_ShouldSucceedForAnyServer()
    {
        // Arrange - Create server with regular user
        var (userToken, userId) = await RegisterAndLoginUserAsync("regularuser", "regular@example.com", "RegularPass123!");
        SetAuthToken(userToken);

        var createResponse = await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "User Server",
            IpAddress = "192.168.8.1",
            Status = ServerStatus.Online
        });
        var createdServer = await createResponse.Content.ReadFromJsonAsync<Server>();

        // Switch to admin
        var (adminToken, adminId) = await RegisterAndLoginUserAsync("admin2", "admin2@example.com", "Admin2Pass123!", "Administrator");
        SetAuthToken(adminToken);

        // Act - Admin updates user's server
        var updatedData = new Server
        {
            Id = createdServer!.Id,
            Name = "Admin Updated",
            IpAddress = "192.168.8.2",
            Status = ServerStatus.Maintenance,
            UserId = userId
        };
        var response = await _client.PutAsJsonAsync($"/api/servers/{createdServer.Id}", updatedData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updatedServer = await response.Content.ReadFromJsonAsync<Server>();
        updatedServer.Should().NotBeNull();
        updatedServer!.Name.Should().Be("Admin Updated");
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task GetServerById_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("notfounduser", "notfound@example.com", "NotFoundPass123!");
        SetAuthToken(token);

        // Act
        var response = await _client.GetAsync("/api/servers/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateServer_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("updatenotfound", "updatenotfound@example.com", "UpdateNotFoundPass123!");
        SetAuthToken(token);

        var updatedData = new Server
        {
            Id = 99999,
            Name = "Non-existent",
            IpAddress = "192.168.9.1",
            Status = ServerStatus.Online,
            UserId = userId
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/servers/99999", updatedData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteServer_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("deletenotfound", "deletenotfound@example.com", "DeleteNotFoundPass123!");
        SetAuthToken(token);

        // Act
        var response = await _client.DeleteAsync("/api/servers/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Status Update Tests

    [Fact]
    public async Task UpdateServerStatus_WithValidStatus_ShouldSucceed()
    {
        // Arrange
        var (token, userId) = await RegisterAndLoginUserAsync("statususer", "status@example.com", "StatusPass123!");
        SetAuthToken(token);

        var createResponse = await _client.PostAsJsonAsync("/api/servers", new Server
        {
            Name = "Status Test Server",
            IpAddress = "192.168.10.1",
            Status = ServerStatus.Offline
        });
        var createdServer = await createResponse.Content.ReadFromJsonAsync<Server>();

        // Act
        var response = await _client.PatchAsync(
            $"/api/servers/{createdServer!.Id}/status",
            JsonContent.Create(ServerStatus.Online));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify status was updated
        var getResponse = await _client.GetAsync($"/api/servers/{createdServer.Id}");
        var server = await getResponse.Content.ReadFromJsonAsync<Server>();
        server!.Status.Should().Be(ServerStatus.Online);
    }

    #endregion
}
