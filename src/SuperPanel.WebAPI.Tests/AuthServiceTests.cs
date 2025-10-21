using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace SuperPanel.WebAPI.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly AuthService _authService;
    private readonly string _databaseName;

    public AuthServiceTests()
    {
        _databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        _context = new ApplicationDbContext(options);
        _configurationMock = new Mock<IConfiguration>();

        // Setup JWT configuration
        _configurationMock.Setup(c => c["Jwt:Key"])
            .Returns("SuperSecretKeyForTestingPurposesOnly12345");
        _configurationMock.Setup(c => c["Jwt:Issuer"])
            .Returns("SuperPanel");
        _configurationMock.Setup(c => c["Jwt:Audience"])
            .Returns("SuperPanelUsers");

        _authService = new AuthService(_context, _configurationMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "TestPassword123!";
        var role = "User";

        // Act
        var result = await _authService.RegisterAsync(username, email, password, role);

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be(username);
        result.Email.Should().Be(email);
        result.Role.Should().Be(role);
        result.IsActive.Should().BeTrue();
        result.PasswordHash.Should().NotBeNullOrEmpty();
        result.PasswordSalt.Should().NotBeNullOrEmpty();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Verify user was saved to database
        var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        savedUser.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateUsername_ShouldThrowException()
    {
        // Arrange
        var username = "testuser";
        var email1 = "test1@example.com";
        var email2 = "test2@example.com";
        var password = "TestPassword123!";

        await _authService.RegisterAsync(username, email1, password);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(username, email2, password));
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ShouldThrowException()
    {
        // Arrange
        var username1 = "testuser1";
        var username2 = "testuser2";
        var email = "test@example.com";
        var password = "TestPassword123!";

        await _authService.RegisterAsync(username1, email, password);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(username2, email, password));
    }

    [Fact]
    public async Task RegisterAsync_WithAdminRole_ShouldCreateAdminUser()
    {
        // Arrange
        var username = "admin";
        var email = "admin@example.com";
        var password = "AdminPassword123!";
        var role = "Administrator";

        // Act
        var result = await _authService.RegisterAsync(username, email, password, role);

        // Assert
        result.Should().NotBeNull();
        result.Role.Should().Be("Administrator");
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ShouldReturnUser()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "TestPassword123!";

        var registeredUser = await _authService.RegisterAsync(username, email, password);

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be(username);
        result.Email.Should().Be(email);
        result.LastLoginAt.Should().NotBeNull();
        result.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidPassword_ShouldReturnNull()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword123!";

        await _authService.RegisterAsync(username, email, password);

        // Act
        var result = await _authService.AuthenticateAsync(username, wrongPassword);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithNonExistentUser_ShouldReturnNull()
    {
        // Act
        var result = await _authService.AuthenticateAsync("nonexistent", "password");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithInactiveUser_ShouldReturnNull()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "TestPassword123!";

        var user = await _authService.RegisterAsync(username, email, password);
        user.IsActive = false;
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UserExistsAsync_WithExistingUsername_ShouldReturnTrue()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "TestPassword123!";

        await _authService.RegisterAsync(username, email, password);

        // Act
        var result = await _authService.UserExistsAsync(username, "other@example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserExistsAsync_WithExistingEmail_ShouldReturnTrue()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "TestPassword123!";

        await _authService.RegisterAsync(username, email, password);

        // Act
        var result = await _authService.UserExistsAsync("otheruser", email);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserExistsAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Act
        var result = await _authService.UserExistsAsync("nonexistent", "nonexistent@example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GenerateJwtToken_ShouldReturnValidToken()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var token = _authService.GenerateJwtToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        // Verify token structure
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == "nameid" && c.Value == "1");
        jwtToken.Claims.Should().Contain(c => c.Type == "unique_name" && c.Value == "testuser");
        jwtToken.Claims.Should().Contain(c => c.Type == "email" && c.Value == "test@example.com");
        jwtToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == "User");
        jwtToken.Issuer.Should().Be("SuperPanel");
        jwtToken.Audiences.Should().Contain("SuperPanelUsers");
    }

    [Fact]
    public void GenerateJwtToken_ForAdminUser_ShouldIncludeAdminRole()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@example.com",
            Role = "Administrator",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var token = _authService.GenerateJwtToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Administrator");
    }

    [Fact]
    public void HashPassword_ShouldGenerateHashAndSalt()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _authService.HashPassword(password, out string salt);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        salt.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password); // Hash should not be plain text
        hash.Length.Should().BeGreaterThan(50); // BCrypt hashes are long
    }

    [Fact]
    public void HashPassword_WithSamePassword_ShouldGenerateDifferentHashes()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _authService.HashPassword(password, out string salt1);
        var hash2 = _authService.HashPassword(password, out string salt2);

        // Assert
        hash1.Should().NotBe(hash2); // Different salts should produce different hashes
        salt1.Should().NotBe(salt2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _authService.HashPassword(password, out string salt);

        // Act
        var result = _authService.VerifyPassword(password, hash, salt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword123!";
        var hash = _authService.HashPassword(password, out string salt);

        // Act
        var result = _authService.VerifyPassword(wrongPassword, hash, salt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldUpdateLastLoginTime()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "TestPassword123!";

        var user = await _authService.RegisterAsync(username, email, password);
        var originalLastLogin = user.LastLoginAt;

        // Wait a moment to ensure time difference
        await Task.Delay(10);

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().NotBeNull();
        result!.LastLoginAt.Should().NotBe(originalLastLogin);
        result.LastLoginAt.Should().BeAfter(user.CreatedAt);
    }

    [Fact]
    public async Task RegisterAsync_MultipleUsers_ShouldAssignUniqueIds()
    {
        // Arrange & Act
        var user1 = await _authService.RegisterAsync("user1", "user1@example.com", "Password1!");
        var user2 = await _authService.RegisterAsync("user2", "user2@example.com", "Password2!");
        var user3 = await _authService.RegisterAsync("user3", "user3@example.com", "Password3!");

        // Assert
        user1.Id.Should().NotBe(user2.Id);
        user2.Id.Should().NotBe(user3.Id);
        user1.Id.Should().NotBe(user3.Id);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
