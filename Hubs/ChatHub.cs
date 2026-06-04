using ChatSupport.Data;
using ChatSupport.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatSupport.Hubs;

/// <summary>
/// Real-time transport for both sides of the conversation.
/// Customers and the handling agent share a SignalR group keyed by the
/// conversation's PublicId. All agents also join the "agents" group so the
/// inbox can refresh live when new chats arrive or get reassigned/closed.
/// </summary>
public class ChatHub : Hub
{
    private const string AgentsGroup = "agents";
    private readonly AppDbContext _db;

    public ChatHub(AppDbContext db) => _db = db;

    /// <summary>Customer (or the agent viewing a conversation) joins the conversation group.</summary>
    public async Task JoinConversation(string publicId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(publicId));
    }

    /// <summary>An agent opens the inbox and subscribes to live inbox updates.</summary>
    public async Task JoinAgents()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AgentsGroup);
    }

    /// <summary>Customer sends a free-text message.</summary>
    public async Task SendCustomerMessage(string publicId, string text)
    {
        var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.PublicId == Guid.Parse(publicId));
        if (convo is null || convo.Status == ConversationStatus.Closed || string.IsNullOrWhiteSpace(text))
            return;

        var msg = await PersistAsync(convo, SenderType.Customer, convo.CustomerName, text);
        await Broadcast(publicId, msg);
        await Clients.Group(AgentsGroup).SendAsync("InboxUpdated");
    }

    /// <summary>Agent replies inside a conversation.</summary>
    public async Task SendAgentMessage(string publicId, string agentName, string text)
    {
        var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.PublicId == Guid.Parse(publicId));
        if (convo is null || convo.Status == ConversationStatus.Closed || string.IsNullOrWhiteSpace(text))
            return;

        var msg = await PersistAsync(convo, SenderType.Agent, agentName, text);
        await Broadcast(publicId, msg);
        await Clients.Group(AgentsGroup).SendAsync("InboxUpdated");
    }

    private async Task<Message> PersistAsync(Conversation convo, SenderType type, string sender, string text)
    {
        var msg = new Message
        {
            ConversationId = convo.Id,
            SenderType = type,
            SenderName = sender,
            Text = text.Trim(),
            SentAt = DateTime.UtcNow
        };
        _db.Messages.Add(msg);
        convo.LastActivityAt = msg.SentAt;
        await _db.SaveChangesAsync();
        return msg;
    }

    private Task Broadcast(string publicId, Message msg) =>
        Clients.Group(ConversationGroup(publicId)).SendAsync("ReceiveMessage", new
        {
            senderType = msg.SenderType.ToString(),
            senderName = msg.SenderName,
            text = msg.Text,
            sentAt = msg.SentAt
        });

    private static string ConversationGroup(string publicId) => $"conv-{publicId}";
}
