using JJ.Commerce;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Controllers;

[ApiController, Route("internal/notifications/invoices"), RequestSizeLimit(8 * 1024)]
[EnableRateLimiting("commercial-authority")]
public sealed class InvoiceNotificationsController(IConfiguration configuration, IInvoiceNotificationSender sender,
    TimeProvider clock, ILogger<InvoiceNotificationsController> logger) : ControllerBase
{
    [HttpPost("send")]
    public Task<IActionResult> Send(InvoiceNotificationCommand command, CancellationToken ct) => Execute(token => sender.SendAsync(command, token), ct);

    [HttpPost("receipt")]
    public Task<IActionResult> Receipt(InvoiceNotificationReceiptCommand command, CancellationToken ct) => Execute(token => sender.ReceiptAsync(command, token), ct);

    private async Task<IActionResult> Execute(Func<CancellationToken, Task<InvoiceNotificationReceipt>> action, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("InvoiceNotifications:Enabled")) return NotFound();
        if (!await CommercialAuthorityBoundary.IsAuthorizedAsync(HttpContext, configuration, clock.GetUtcNow().UtcDateTime)) return StatusCode(403);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try { return Ok(await action(deadline.Token)); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (ArgumentException) { return BadRequest(); }
        catch (SqlException error)
        {
            var status = SqlStatus(error.Number);
            logger.LogWarning("Invoice notification SQL result {Code}; status {Status}; trace {Trace}", error.Number, status, HttpContext.TraceIdentifier);
            return StatusCode(status);
        }
        catch (Exception error) when (error is InvalidOperationException or OperationCanceledException or IOException)
        {
            logger.LogWarning("Invoice notification unconfirmed; type {Type}; trace {Trace}", error.GetType().Name, HttpContext.TraceIdentifier);
            return StatusCode(503);
        }
    }

    internal static int SqlStatus(int code) => code switch
    {
        59304 => 404, 59303 => 409, 59301 => 400,
        56801 or 56802 or 56701 or 56702 => 403, _ => 503
    };
}
