using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Controllers;

[ApiController, Route("internal/notifications/contact"), RequestSizeLimit(16 * 1024)]
[EnableRateLimiting("contact-notification")]
public sealed class ContactNotificationsController(IConfiguration configuration, IContactNotificationSender sender,
    TimeProvider clock, ILogger<ContactNotificationsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send(ContactNotificationRequest request, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Contact:NotificationsEnabled")) return NotFound();
        if (!await CommercialAuthorityBoundary.IsAuthorizedAsync(HttpContext, configuration, clock.GetUtcNow().UtcDateTime))
            return StatusCode(403);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await sender.SendAsync(request, deadline.Token);
            // SMTP acceptance is distinct from delivery to an inbox. The caller must not replay an unknown outcome.
            return Ok(new { request.ConsultationId, status = "smtpAccepted" });
        }
        catch (Exception e) when (e is SmtpException or InvalidOperationException or OperationCanceledException)
        {
            logger.LogWarning("Contact notification unconfirmed. Type {Type}; Trace {Trace}", e.GetType().Name, HttpContext.TraceIdentifier);
            return StatusCode(503);
        }
    }
}
