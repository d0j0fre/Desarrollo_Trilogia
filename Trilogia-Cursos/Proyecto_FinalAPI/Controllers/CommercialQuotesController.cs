using JJ.Commerce;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Controllers;

[ApiController, Route("internal/commerce/quotes")]
[RequestSizeLimit(128 * 1024)]
[EnableRateLimiting("commercial-authority")]
public sealed class CommercialQuotesController(IConfiguration configuration, ICommercialQuoteAuthority authority,
    TimeProvider clock, ILogger<CommercialQuotesController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Issue(ServerQuoteCommand command, CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Commerce:Enabled")) return NotFound();
        if (!await CommercialAuthorityBoundary.IsAuthorizedAsync(HttpContext, configuration, clock.GetUtcNow().UtcDateTime))
            return StatusCode(403);
        if (command.Session is null || !command.Session.IsValid(clock.GetUtcNow().UtcDateTime)) return Unauthorized();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try { return Ok(await authority.IssueAsync(command, deadline.Token)); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (ArgumentException) { return BadRequest(); }
        catch (OverflowException) { return BadRequest(); }
        catch (CommercialBenefitUnavailableException) { return Conflict(); }
        catch (SqlException error)
        {
            var status = error.Number switch
            {
                56801 => 401, 56802 => 403, 58801 => 400,
                58702 or 58803 or 58805 or 58806 => 409, _ => 503
            };
            if (status == 503) logger.LogWarning("Pricing SQL dependency failed: {Code}; Trace {Trace}", error.Number, HttpContext.TraceIdentifier);
            return StatusCode(status);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return StatusCode(503); }
        catch (InvalidOperationException)
        {
            logger.LogWarning("Pricing source contract unavailable; Trace {Trace}", HttpContext.TraceIdentifier);
            return StatusCode(503);
        }
    }
}
