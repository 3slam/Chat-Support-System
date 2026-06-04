using ChatSupport.Data;
using ChatSupport.Hubs;
using ChatSupport.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatSupport.Controllers;

/// <summary>
/// Customer-facing JSON API that drives the pre-chat bot flow and conversation
/// lifecycle. Real-time text after hand-off flows through <see cref="ChatHub"/>.
/// </summary>
[ApiController]
[Route("api/chat")]
public class ChatApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public ChatApiController(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public record StartRequest(string? Name, string? Email, string Message);
    public record CategoryRequest(string Category);
    public record SubOptionRequest(string SubOption);

    /// <summary>Open a chat (with or without login) and submit the opening free-text message.</summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message is required." });

        var name = string.IsNullOrWhiteSpace(req.Name) ? "Guest" : req.Name.Trim();

        var convo = new Conversation
        {
            CustomerName = name,
            CustomerEmail = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            IsAuthenticated = !string.IsNullOrWhiteSpace(req.Email),
            InitialMessage = req.Message.Trim(),
            Status = ConversationStatus.Bot
        };
        _db.Conversations.Add(convo);
        await _db.SaveChangesAsync();

        AddMessage(convo, SenderType.Customer, name, req.Message.Trim());
        AddMessage(convo, SenderType.Bot, "TripsBot","أهلاً بك في الدعم! من فضلك اختر الخدمة المطلوبة. / Welcome! Please pick a service.");
        await _db.SaveChangesAsync();

        return Ok(new { publicId = convo.PublicId });
    }

    /// <summary>Customer picks one of the four categories.</summary>
    [HttpPost("{publicId:guid}/category")]
    public async Task<IActionResult> SetCategory(Guid publicId, [FromBody] CategoryRequest req)
    {
        var convo = await Load(publicId);
        if (convo is null) return NotFound();
        if (!Enum.TryParse<ChatCategory>(req.Category, true, out var category))
            return BadRequest(new { error = "Unknown category." });

        convo.Category = category;
        AddMessage(convo, SenderType.Customer, convo.CustomerName, category.ToString());
        AddMessage(convo, SenderType.Bot, "TripsBot",
            "كيف يمكننا مساعدتك؟ / How can we help?");
        await _db.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    /// <summary>Customer picks a sub-option; conversation drops into the agent inbox.</summary>
    [HttpPost("{publicId:guid}/suboption")]
    public async Task<IActionResult> SetSubOption(Guid publicId, [FromBody] SubOptionRequest req)
    {
        var convo = await Load(publicId);
        if (convo is null) return NotFound();
        if (!Enum.TryParse<SubOption>(req.SubOption, true, out var sub))
            return BadRequest(new { error = "Unknown sub-option." });

        var label = sub == SubOption.GeneralInquiry ? "استفسار عام" : "لدي مشكلة مع الحجز";

        convo.SubOption = sub;
        convo.Status = ConversationStatus.Waiting;
        convo.LastActivityAt = DateTime.UtcNow;
        AddMessage(convo, SenderType.Customer, convo.CustomerName, label);
        AddMessage(convo, SenderType.Bot, "TripsBot",
            "جارٍ تحويلك إلى أحد موظفينا. / Connecting you to an agent…");
        await _db.SaveChangesAsync();

        // Wake up every open inbox.
        await _hub.Clients.Group("agents").SendAsync("InboxUpdated");

        return Ok(new { ok = true });
    }

    /// <summary>Full conversation state — used by the customer widget to reconnect.</summary>
    [HttpGet("{publicId:guid}")]
    public async Task<IActionResult> Get(Guid publicId)
    {
        var convo = await _db.Conversations
            .Include(c => c.Messages)
            .Include(c => c.AssignedAdmin)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == publicId);
        if (convo is null) return NotFound();

        return Ok(new
        {
            publicId = convo.PublicId,
            status = convo.Status.ToString(),
            category = convo.Category?.ToString(),
            agentName = convo.AssignedAdmin?.FullName,
            messages = convo.Messages.OrderBy(m => m.SentAt).Select(m => new
            {
                senderType = m.SenderType.ToString(),
                senderName = m.SenderName,
                text = m.Text,
                sentAt = m.SentAt
            })
        });
    }

    private Task<Conversation?> Load(Guid publicId) =>
        _db.Conversations.FirstOrDefaultAsync(c => c.PublicId == publicId);

    private void AddMessage(Conversation convo, SenderType type, string sender, string text)
    {
        _db.Messages.Add(new Message
        {
            ConversationId = convo.Id,
            SenderType = type,
            SenderName = sender,
            Text = text,
            SentAt = DateTime.UtcNow
        });
    }
}
