using JJ.Commerce;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Controllers;

[ApiController, Route("internal/notifications/pay-slips"), RequestSizeLimit(8 * 1024)]
[EnableRateLimiting("commercial-authority")]
public sealed class PaySlipNotificationsController(IConfiguration configuration, TimeProvider clock,
    ILogger<PaySlipNotificationsController> logger, IPaySlipNotificationSender? sender = null) : ControllerBase
{
    [HttpPost("send")]
    public Task<IActionResult> Send(PaySlipNotificationCommand command, CancellationToken ct)
        => Execute((authority, token) => authority.SendAsync(command, token), ct);

    [HttpPost("receipt")]
    public Task<IActionResult> Receipt(PaySlipNotificationReceiptCommand command, CancellationToken ct)
        => Execute((authority, token) => authority.ReceiptAsync(command, token), ct);

    private async Task<IActionResult> Execute(Func<IPaySlipNotificationSender, CancellationToken, Task<PaySlipNotificationReceipt>> action, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("PaySlipNotifications:Enabled")) return NotFound();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, HttpContext.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            if (!await CommercialAuthorityBoundary.IsAuthorizedAsync(HttpContext, configuration, clock.GetUtcNow().UtcDateTime, deadline.Token))
                return StatusCode(403);
            if (sender is null) return StatusCode(503);
            return Ok(await action(sender, deadline.Token));
        }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (ArgumentException) { return BadRequest(); }
        catch (SqlException error)
        {
            int status = SqlStatus(error.Number);
            logger.LogWarning("Pay slip notification SQL result {Code}; status {Status}; trace {Trace}", error.Number, status, HttpContext.TraceIdentifier);
            return StatusCode(status);
        }
        catch (Exception error) when (error is InvalidOperationException or OperationCanceledException or IOException)
        {
            logger.LogWarning("Pay slip notification unconfirmed; type {Type}; trace {Trace}", error.GetType().Name, HttpContext.TraceIdentifier);
            return StatusCode(503);
        }
    }

    internal static int SqlStatus(int code) => code switch
    {
        59604 => 404, 59603 => 409, 59601 => 400,
        56801 or 56802 or 56701 or 56702 => 403, _ => 503
    };
}
