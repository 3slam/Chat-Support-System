using System.ComponentModel.DataAnnotations;

namespace ChatSupport.Models;

/// <summary>A single line in a conversation transcript.</summary>
public class Message
{
    public int Id { get; set; }

    public int ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public SenderType SenderType { get; set; }

    [MaxLength(120)]
    public string SenderName { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Text { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
