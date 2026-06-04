using System.Security.Claims;
using ChatSupport.Data;
using ChatSupport.Hubs;
using ChatSupport.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatSupport.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public AdminController(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private int CurrentAdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentAdminName => User.FindFirstValue(ClaimTypes.Name)!;

    private static readonly string[] Tabs = { "All", "Flights", "Hotel", "Visa", "Activity" };

    /// <summary>Inbox shell with the 5 tabs. Rows are loaded/refreshed via <see cref="InboxData"/>.</summary>
    public IActionResult Inbox(string tab = "All")
    {
        if (!Tabs.Contains(tab)) tab = "All";
        ViewBag.Tab = tab;
        ViewBag.Tabs = Tabs;
        ViewBag.AdminName = CurrentAdminName;
        return View();
    }

    /// <summary>JSON feed of conversations for a given tab (drives live inbox refresh).</summary>
    [HttpGet]
    public async Task<IActionResult> InboxData(string tab = "All")
    {
        IQueryable<Conversation> q = _db.Conversations
            .Include(c => c.AssignedAdmin);

        var category = TabToCategory(tab);
        if (category is not null)
            q = q.Where(c => c.Category == category);

        var rows = await q
            .OrderByDescending(c => c.LastActivityAt)
            .Select(c => new
            {
                id = c.Id,
                customer = c.CustomerName,
                category = c.Category!.Value.ToString(),
                subOption = c.SubOption!.Value.ToString(),
                status = c.Status.ToString(),
                assignedTo = c.AssignedAdmin != null ? c.AssignedAdmin.FullName : null,
                assignedToMe = c.AssignedAdminId == CurrentAdminId,
                lastActivity = c.LastActivityAt,
                preview = c.Messages.OrderByDescending(m => m.SentAt)
                    .Select(m => m.Text).FirstOrDefault()
            })
            .ToListAsync();

        return Json(rows);
    }

    /// <summary>Open a single conversation (agent workspace).</summary>
    public async Task<IActionResult> Conversation(int id)
    {
        var convo = await _db.Conversations
            .Include(c => c.Messages)
            .Include(c => c.AssignedAdmin)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (convo is null) return NotFound();

        ViewBag.AdminId = CurrentAdminId;
        ViewBag.AdminName = CurrentAdminName;
        ViewBag.Admins = await _db.Admins
            .OrderBy(a => a.FullName)
            .Select(a => new { a.Id, a.FullName })
            .ToListAsync();
        return View(convo);
    }

    /// <summary>Auto-assignment: the agent who picks up an unassigned chat owns it.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pickup(int id)
    {
        var convo = await _db.Conversations.FindAsync(id);
        if (convo is null) return NotFound();
        if (convo.Status == ConversationStatus.Closed)
            return BadRequest(new { error = "Conversation is closed." });

        convo.AssignedAdminId = CurrentAdminId;
        convo.Status = ConversationStatus.Assigned;
        convo.LastActivityAt = DateTime.UtcNow;
        _db.Messages.Add(SystemMessage(convo, $"{CurrentAdminName} joined the chat."));
        await _db.SaveChangesAsync();

        await NotifyConversation(convo.PublicId, $"{CurrentAdminName} joined the chat.", convo.Status, CurrentAdminName);
        await _hub.Clients.Group("agents").SendAsync("InboxUpdated");
        return RedirectToAction(nameof(Conversation), new { id });
    }

    /// <summary>Admin reassigns a conversation to a different agent.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reassign(int id, int adminId)
    {
        var convo = await _db.Conversations.FindAsync(id);
        if (convo is null) return NotFound();

        var target = await _db.Admins.FindAsync(adminId);
        if (target is null) return BadRequest(new { error = "Unknown agent." });

        convo.AssignedAdminId = target.Id;
        if (convo.Status != ConversationStatus.Closed)
            convo.Status = ConversationStatus.Assigned;
        convo.LastActivityAt = DateTime.UtcNow;
        _db.Messages.Add(SystemMessage(convo, $"Reassigned to {target.FullName} by {CurrentAdminName}."));
        await _db.SaveChangesAsync();

        await NotifyConversation(convo.PublicId, $"You're now chatting with {target.FullName}.", convo.Status, target.FullName);
        await _hub.Clients.Group("agents").SendAsync("InboxUpdated");
        return RedirectToAction(nameof(Conversation), new { id });
    }

    /// <summary>Admin closes the conversation.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var convo = await _db.Conversations.FindAsync(id);
        if (convo is null) return NotFound();

        convo.Status = ConversationStatus.Closed;
        convo.ClosedAt = DateTime.UtcNow;
        convo.LastActivityAt = DateTime.UtcNow;
        _db.Messages.Add(SystemMessage(convo, $"Conversation closed by {CurrentAdminName}."));
        await _db.SaveChangesAsync();

        await NotifyConversation(convo.PublicId, "This conversation has been closed.", convo.Status, convo.AssignedAdmin?.FullName);
        await _hub.Clients.Group("agents").SendAsync("InboxUpdated");
        return RedirectToAction(nameof(Inbox));
    }

    private Task NotifyConversation(Guid publicId, string systemText, ConversationStatus status, string? agentName) =>
        _hub.Clients.Group($"conv-{publicId}").SendAsync("ConversationUpdated", new
        {
            status = status.ToString(),
            agentName,
            systemText
        });

    private Message SystemMessage(Conversation convo, string text) => new()
    {
        ConversationId = convo.Id,
        SenderType = SenderType.Bot,
        SenderName = "System",
        Text = text,
        SentAt = DateTime.UtcNow
    };

    private static ChatCategory? TabToCategory(string tab) => tab switch
    {
        "Flights" => ChatCategory.Flight,
        "Hotel" => ChatCategory.Hotel,
        "Visa" => ChatCategory.Visa,
        "Activity" => ChatCategory.Activity,
        _ => null
    };
}
