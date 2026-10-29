using JJ.Commerce;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Controllers;

[ApiController, Route("internal/notifications/route-dispatch"), RequestSizeLimit(8 * 1024)]
[EnableRateLimiting("commercial-authority")]
public sealed class RouteDispatchNotificationsController(IConfiguration configuration, IRouteDispatchNotificationSender sender,
    TimeProvider clock, ILogger<RouteDispatchNotificationsController> logger) : ControllerBase
{
    [HttpPost("send")]
    public Task<IActionResult> Send(RouteDispatchNotificationCommand command, CancellationToken ct) => Execute(token => sender.SendAsync(command, token), ct);
    [HttpPost("receipt")]
    public Task<IActionResult> Receipt(RouteDispatchNotificationReceiptCommand command, CancellationToken ct) => Execute(token => sender.ReceiptAsync(command, token), ct);

    private async Task<IActionResult> Execute(Func<CancellationToken, Task<RouteDispatchNotificationReceipt>> action, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("RouteDispatchNotifications:Enabled")) return NotFound();
        if (!await CommercialAuthorityBoundary.IsAuthorizedAsync(HttpContext, configuration, clock.GetUtcNow().UtcDateTime)) return StatusCode(403);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try { return Ok(await action(deadline.Token)); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (ArgumentException) { return BadRequest(); }
        catch (SqlException error)
        {
            int status = SqlStatus(error.Number);
            logger.LogWarning("Dispatch notification SQL result {Code}; status {Status}; trace {Trace}", error.Number, status, HttpContext.TraceIdentifier);
            return StatusCode(status);
        }
        catch (Exception error) when (error is InvalidOperationException or OperationCanceledException or IOException)
        {
            logger.LogWarning("Dispatch notification unconfirmed; type {Type}; trace {Trace}", error.GetType().Name, HttpContext.TraceIdentifier);
            return StatusCode(503);
        }
    }
    internal static int SqlStatus(int code) => code switch
    {
        59501 or 59504 => 400, 59502 => 404, 59503 or 59505 => 409,
        56801 or 56802 => 403, _ => 503
    };
}
