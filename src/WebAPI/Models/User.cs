using System.ComponentModel.DataAnnotations;

namespace SuperPanel.WebAPI.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string PasswordSalt { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Role { get; set; } = "User"; // Admin, User

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }
}

public enum UserRole
{
    Admin,
    User
}