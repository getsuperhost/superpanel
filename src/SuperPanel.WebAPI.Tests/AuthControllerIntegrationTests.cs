using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SuperPanel.WebAPI.Controllers;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Hubs;
using SuperPanel.WebAPI.Services;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

/// <summary>
/// Test web application factory for integration tests
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}

/// <summary>
/// Integration tests for AuthController endpoints.
/// Tests the full HTTP pipeline including authentication, routing, and database integration.
/// </summary>
public class AuthControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    #region Registration Tests

    [Fact]
    public async Task Register_WithValidData_ShouldReturnOkWithToken()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "TestPass123!",
            Role = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<AuthResponse>();
        content.Should().NotBeNull();
        content!.Token.Should().NotBeNullOrEmpty();
        content.User.Should().NotBeNull();
        content.User.Username.Should().Be("testuser");
        content.User.Email.Should().Be("test@example.com");
        content.User.Role.Should().Be("User");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldReturnBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Username = "duplicate",
            Email = "first@example.com",
            Password = "TestPass123!",
            Role = "User"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var duplicateRequest = new RegisterRequest
        {
            Username = "duplicate",
            Email = "second@example.com",
            Password = "TestPass456!",
            Role = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Username = "user1",
            Email = "duplicate@example.com",
            Password = "TestPass123!",
            Role = "User"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var duplicateRequest = new RegisterRequest
        {
            Username = "user2",
            Email = "duplicate@example.com",
            Password = "TestPass456!",
            Role = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithAdminRole_ShouldCreateAdminUser()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Username = "adminuser",
            Email = "admin@example.com",
            Password = "AdminPass123!",
            Role = "Administrator"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<AuthResponse>();
        content.Should().NotBeNull();
        content!.User.Role.Should().Be("Administrator");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOkWithToken()
    {
        // Arrange - Register user first
        var registerRequest = new RegisterRequest
        {
            Username = "logintest",
            Email = "login@example.com",
            Password = "LoginPass123!",
            Role = "User"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Username = "logintest",
            Password = "LoginPass123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<AuthResponse>();
        content.Should().NotBeNull();
        content!.Token.Should().NotBeNullOrEmpty();
        content.User.Username.Should().Be("logintest");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        // Arrange - Register user first
        var registerRequest = new RegisterRequest
        {
            Username = "invalidpass",
            Email = "invalidpass@example.com",
            Password = "CorrectPass123!",
            Role = "User"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Username = "invalidpass",
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "nonexistent",
            Password = "AnyPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ShouldReturnUnauthorized()
    {
        // Arrange - Register user first
        var registerRequest = new RegisterRequest
        {
            Username = "inactiveuser",
            Email = "inactive@example.com",
            Password = "InactivePass123!",
            Role = "User"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Deactivate user directly in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == "inactiveuser");
        user!.IsActive = false;
        await db.SaveChangesAsync();

        var loginRequest = new LoginRequest
        {
            Username = "inactiveuser",
            Password = "InactivePass123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Protected Endpoint Tests

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ShouldReturnUserInfo()
    {
        // Arrange - Register and login to get token
        var registerRequest = new RegisterRequest
        {
            Username = "protectedtest",
            Email = "protected@example.com",
            Password = "ProtectedPass123!",
            Role = "User"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Add token to request
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<UserDto>();
        content.Should().NotBeNull();
        content!.Username.Should().Be("protectedtest");
        content.Email.Should().Be("protected@example.com");
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange - Use invalid token
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region End-to-End Authentication Flow Tests

    [Fact]
    public async Task FullAuthenticationFlow_RegisterLoginAndAccessProtectedEndpoint_ShouldSucceed()
    {
        // Step 1: Register
        var registerRequest = new RegisterRequest
        {
            Username = "flowtest",
            Email = "flow@example.com",
            Password = "FlowPass123!",
            Role = "User"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var registerAuth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        registerAuth.Should().NotBeNull();
        registerAuth!.Token.Should().NotBeNullOrEmpty();

        // Step 2: Login with same credentials
        var loginRequest = new LoginRequest
        {
            Username = "flowtest",
            Password = "FlowPass123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loginAuth.Should().NotBeNull();
        loginAuth!.Token.Should().NotBeNullOrEmpty();

        // Step 3: Access protected endpoint with login token
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginAuth.Token);
        
        var meResponse = await _client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var userInfo = await meResponse.Content.ReadFromJsonAsync<UserDto>();
        userInfo.Should().NotBeNull();
        userInfo!.Username.Should().Be("flowtest");
        userInfo.Email.Should().Be("flow@example.com");
        userInfo.Role.Should().Be("User");
    }

    #endregion
}
