using System.ComponentModel.DataAnnotations;

namespace ChatSupport.Models;

/// <summary>A single customer chat session.</summary>
public class Conversation
{
    public int Id { get; set; }

    /// <summary>Opaque id handed to the (possibly anonymous) customer so they can reconnect.</summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string CustomerName { get; set; } = "Guest";

    [MaxLength(200)]
    public string? CustomerEmail { get; set; }

    /// <summary>True when the customer was logged in, false for an anonymous guest.</summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>The customer's opening free-text message.</summary>
    [MaxLength(2000)]
    public string? InitialMessage { get; set; }

    public ChatCategory? Category { get; set; }

    public SubOption? SubOption { get; set; }

    public ConversationStatus Status { get; set; } = ConversationStatus.Bot;

    public int? AssignedAdminId { get; set; }
    public Admin? AssignedAdmin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
