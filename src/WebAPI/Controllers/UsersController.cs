using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Controllers;
using SuperPanel.WebAPI.Services;

namespace SuperPanel.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;

    public UsersController(ApplicationDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var user = await _authService.RegisterAsync(
                request.Username,
                request.Email,
                request.Password,
                request.Role ?? "User");

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the user" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        // Update fields
        if (!string.IsNullOrEmpty(request.Email))
            user.Email = request.Email;

        if (!string.IsNullOrEmpty(request.Role))
            user.Role = request.Role;

        // Only update password if provided
        if (!string.IsNullOrEmpty(request.Password))
        {
            user.PasswordHash = _authService.HashPassword(request.Password, out string salt);
            user.PasswordSalt = salt;
        }

        await _context.SaveChangesAsync();

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        // Prevent deleting the last administrator
        if (user.Role == "Administrator")
        {
            var adminCount = await _context.Users.CountAsync(u => u.Role == "Administrator");
            if (adminCount <= 1)
            {
                return BadRequest(new { message = "Cannot delete the last administrator" });
            }
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

// Request DTOs
public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Role { get; set; }
}

public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
}