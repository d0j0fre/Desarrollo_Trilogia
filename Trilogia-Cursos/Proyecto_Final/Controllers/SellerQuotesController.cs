using System.Text.Json.Serialization;
using JJ.Commerce;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SellerQuoteDraft(Guid RequestId, int Customer, string Delivery, string Address,
    string? Notes, string? Identification, IReadOnlyList<OrderQuantity> Items);

// Price preview is an actual business operation, not a test or identity-issuing endpoint.
// It never creates an order, consumes stock or accepts a browser-supplied price/session.
[AdminAuthorize("Venta móvil", "VENTA_MOVIL_CREAR", false)]
public sealed class SellerQuotesController(IConfiguration configuration, ICommercialQuoteClient authority,
    ILogger<SellerQuotesController> logger) : Controller
{
    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive-read")]
    [RequestSizeLimit(128 * 1024)]
    public Task<IActionResult> Preview([FromBody] SellerQuoteDraft? draft, CancellationToken cancellationToken) =>
        Issue(draft, "Venta móvil", cancellationToken);

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive-read")]
    [RequestSizeLimit(128 * 1024)]
    public Task<IActionResult> PreviewOffline([FromBody] SellerQuoteDraft? draft, CancellationToken cancellationToken) =>
        Issue(draft, "Venta móvil offline", cancellationToken);

    private async Task<IActionResult> Issue(SellerQuoteDraft? draft, string serverChannel, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Commerce:Enabled")) return NotFound();
        if (!ModelState.IsValid || draft is null) return BadRequest();
        try
        {
            var intent = new SellerQuoteIntent(draft.RequestId, draft.Customer, draft.Delivery, draft.Address,
                draft.Notes, draft.Identification, serverChannel, draft.Items).Normalize();
            return Ok(await authority.IssueAsync(intent, ct));
        }
        catch (ArgumentException) { return BadRequest(); }
        catch (OverflowException) { return BadRequest(); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (CommercialQuoteTransportException error)
        {
            return StatusCode(error.Status is 400 or 401 or 403 or 404 or 409 or 429 ? error.Status : 503);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Commercial price transport unavailable; Trace {Trace}", HttpContext.TraceIdentifier);
            return StatusCode(503);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(503); }
    }
}
