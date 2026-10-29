using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;
using Proyecto_Final.Services.ChatV2;

namespace Proyecto_Final.Controllers;

[ServiceFilter(typeof(ChatV2OriginalLoginFilter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ServiceFilter(typeof(ChatV2JsonExceptionFilter))]
public sealed class ChatV2Controller(ChatV2Continuity continuity, IChatV2IntentPersistence persistence,
    SqlLegacyPersonalArchive? archive = null, IPersonalChatV2InvalidationPublisher? notifications = null) : Controller
{
    private OriginalLoginContext Login() => OriginalLoginSession.ValidatedRequest(HttpContext);
    private static ChatV2Resource Resource(string domain, int id)
    {
        var resource = new ChatV2Resource(domain switch { "P" => ChatV2Domain.Private, "D" => ChatV2Domain.Department,
            "C" => ChatV2Domain.Customer, _ => throw new ChatV2Exception(ChatV2Failure.Validation) }, id);
        resource.Validate(); return resource;
    }
    private static void Domain(ChatV2LiveIdentity actor, ChatV2Domain domain)
    {
        var allowed = domain == ChatV2Domain.Customer
            ? actor.Kind == ChatV2Kind.Client && actor.Capabilities.HasFlag(ChatV2Capabilities.CustomerUse) ||
              actor.Kind == ChatV2Kind.Internal && actor.Capabilities.HasFlag(ChatV2Capabilities.AttendCustomers)
            : actor.Kind == ChatV2Kind.Internal && actor.Capabilities.HasFlag(ChatV2Capabilities.InternalUse);
        if (!allowed) throw new ChatV2Exception(ChatV2Failure.Forbidden);
    }
    private static void Management(ChatV2LiveIdentity actor)
    {
        if (actor.Kind != ChatV2Kind.Internal || !actor.Capabilities.HasFlag(ChatV2Capabilities.ManageAttention))
            throw new ChatV2Exception(ChatV2Failure.Forbidden);
    }
    private static object Receipt(ChatV2Receipt r) => new { r.RequestId, Domain = r.Domain switch
        { ChatV2Domain.Private => "P", ChatV2Domain.Department => "D", _ => "C" }, r.ResourceId,
        MessageId = r.MessageId.ToString(CultureInfo.InvariantCulture), StreamSequence = r.StreamSequence.ToString(CultureInfo.InvariantCulture),
        r.Content, r.PersistedState, r.TimestampUtc };
    [HttpGet]
    public IActionResult Index() { _ = Login(); return View(); }
    [HttpGet]
    public async Task<IActionResult> Archive(int? conversationId, int page = 1, CancellationToken ct = default)
    {
        if (archive is null) throw new ChatV2Exception(ChatV2Failure.Dependency);
        if (page is < 1 or > 100000 || conversationId is <= 0) return BadRequest();
        var login = Login();
        var conversations = await archive.ListAsync(login, ct);
        var selected = conversationId ?? (conversations.Count > 0 ? conversations[0] : (int?)null);
        var messages = selected.HasValue ? await archive.ReadAsync(login, selected.Value, page, 50, ct)
            : Array.Empty<Proyecto_Final.Models.Chat.ChatMessageViewModel>();
        return View(new Proyecto_Final.Models.Chat.LegacyPersonalArchiveViewModel(conversations, selected, page, messages));
    }
    [HttpGet]
    public async Task<IActionResult> LegacyHistory(int? conversationId, int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        if (archive is null) throw new ChatV2Exception(ChatV2Failure.Dependency);
        var login = Login();
        if (!conversationId.HasValue) return Json(new { conversations = await archive.ListAsync(login, ct) });
        return Json(new { messages = await archive.ReadAsync(login, conversationId.Value, page, pageSize, ct) });
    }
    [HttpGet]
    public async Task<IActionResult> Context(CancellationToken ct) => Json(await continuity.ContextAsync(Login(), persistence, ct));
    [HttpGet]
    public async Task<IActionResult> Directory(string contextHandle, CancellationToken ct)
    {
        var login = Login();
        Domain(await continuity.ValidateContextAsync(login, contextHandle, persistence, ct), ChatV2Domain.Private);
        return Json(await persistence.DirectoryAsync(login, ct));
    }
    public sealed record OpenPrivateInput(string ContextHandle, int OtherUser);
    [HttpPost, ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("chat-send")]
    public async Task<IActionResult> OpenPrivate([FromBody] OpenPrivateInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.OtherUser <= 0) return BadRequest(new { code = "validation" });
        var login = Login();
        Domain(await continuity.ValidateContextAsync(login, input.ContextHandle, persistence, ct), ChatV2Domain.Private);
        return Json(new { resourceId = await persistence.OpenPrivateAsync(login, input.OtherUser, ct) });
    }
    public sealed record LeaveInput(string ContextHandle, int Resource);
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave([FromBody] LeaveInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.Resource <= 0) return BadRequest(new { code = "validation" });
        var login = Login();
        Domain(await continuity.ValidateContextAsync(login, input.ContextHandle, persistence, ct), ChatV2Domain.Private);
        await persistence.LeaveAsync(login, new(ChatV2Domain.Private, input.Resource), ct);
        return NoContent();
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Prepare([FromBody] ChatV2IntentInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { code = "validation" });
        return Json(await continuity.PrepareAsync(Login(), input, persistence, ct));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send([FromBody] ChatV2IntentInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { code = "validation" });
        var receipt = await continuity.ExecuteAsync(Login(), input, false, persistence, ct);
        notifications?.Publish(new(receipt.Domain, receipt.ResourceId));
        return Json(Receipt(receipt));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Receipt([FromBody] ChatV2IntentInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { code = "validation" });
        return Json(Receipt(await continuity.ExecuteAsync(Login(), input, true, persistence, ct)));
    }
    [HttpGet]
    public async Task<IActionResult> Own(string contextHandle, string domain, CancellationToken ct)
    {
        var login = Login(); var resource = Resource(domain, 1);
        var actor = await continuity.ValidateContextAsync(login, contextHandle, persistence, ct); Domain(actor, resource.Domain);
        var rows = await persistence.ListAsync(login, resource.Domain, ct);
        if (rows.Any(r => r.Domain != resource.Domain || r.ResourceId <= 0)) throw new ChatV2Exception(ChatV2Failure.Contract);
        return Json(rows.Select(r => new { Domain = domain, r.ResourceId }));
    }
    [HttpGet]
    public async Task<IActionResult> Membership(string contextHandle, string domain, int resource, CancellationToken ct)
    {
        var login = Login(); var selected = Resource(domain, resource);
        Domain(await continuity.ValidateContextAsync(login, contextHandle, persistence, ct), selected.Domain);
        var access = await persistence.MembershipAsync(login, selected, ct);
        if (!access.CanRead) throw new ChatV2Exception(ChatV2Failure.Resource);
        return Json(new { authorized = true, access.CanSend });
    }
    [HttpGet]
    public async Task<IActionResult> History(string contextHandle, string domain, int resource, string? cursor, CancellationToken ct)
    {
        var login = Login(); var selected = Resource(domain, resource);
        Domain(await continuity.ValidateContextAsync(login, contextHandle, persistence, ct), selected.Domain);
        var history = await persistence.HistoryAsync(login, selected, cursor, ct);
        return Json(new { messages = history.Messages.Select(r => new { MessageId = r.MessageId.ToString(CultureInfo.InvariantCulture),
            StreamSequence = r.StreamSequence.ToString(CultureInfo.InvariantCulture), r.Content, r.TimestampUtc }), history.NextCursor });
    }
    [HttpGet]
    public async Task<IActionResult> Queue(string contextHandle, CancellationToken ct)
    {
        var login = Login(); Management(await continuity.ValidateContextAsync(login, contextHandle, persistence, ct));
        return Json((await persistence.QueueAsync(login, ct)).Select(r => new { r.CaseId, r.Assigned,
            AssignmentVersion = r.AssignmentVersion.ToString(CultureInfo.InvariantCulture), r.State }));
    }
    public sealed record TransferInput(string ContextHandle, int CaseId, int NewAgent, long ExpectedVersion);
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer([FromBody] TransferInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.CaseId <= 0 || input.NewAgent <= 0 || input.ExpectedVersion < 0) return BadRequest(new { code = "validation" });
        var login = Login(); var actor = await continuity.ValidateContextAsync(login, input.ContextHandle, persistence, ct); Management(actor);
        if (input.NewAgent == actor.Actor) throw new ChatV2Exception(ChatV2Failure.Forbidden);
        var version = await persistence.TransferAsync(login, input.CaseId, input.NewAgent, input.ExpectedVersion, ct);
        return Json(new { assignmentVersion = version.ToString(CultureInfo.InvariantCulture) });
    }
}
