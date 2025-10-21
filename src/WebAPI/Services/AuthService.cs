using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;

namespace SuperPanel.WebAPI.Services;

public interface IAuthService
{
    Task<User?> AuthenticateAsync(string username, string password);
    Task<User> RegisterAsync(string username, string email, string password, string role = "User");
    Task<bool> UserExistsAsync(string username, string email);
    string GenerateJwtToken(User user);
    string HashPassword(string password, out string salt);
    bool VerifyPassword(string password, string hash, string salt);
}

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
            return null;

        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
            return null;

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<User> RegisterAsync(string username, string email, string password, string role = "User")
    {
        if (await UserExistsAsync(username, email))
            throw new InvalidOperationException("User already exists");

        var passwordHash = HashPassword(password, out string salt);

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            PasswordSalt = salt,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<bool> UserExistsAsync(string username, string email)
    {
        return await _context.Users
            .AnyAsync(u => u.Username == username || u.Email == email);
    }

    public string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string HashPassword(string password, out string salt)
    {
        salt = BCrypt.Net.BCrypt.GenerateSalt();
        return BCrypt.Net.BCrypt.HashPassword(password, salt);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}