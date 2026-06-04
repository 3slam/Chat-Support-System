using System.ComponentModel.DataAnnotations;

namespace ChatSupport.Models;

/// <summary>A back-office user (agent / admin) who handles conversations.</summary>
public class Admin
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the password (MVP-grade; use ASP.NET Identity for production).</summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public AdminRole Role { get; set; } = AdminRole.Admin;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Conversation> AssignedConversations { get; set; } = new List<Conversation>();
}
