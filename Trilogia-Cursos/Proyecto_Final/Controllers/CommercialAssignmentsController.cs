using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[AdminAuthorize("Comercial", "RELACIONES_COMERCIALES_GESTIONAR", allowAdministratorBypass: false)]
public sealed class CommercialAssignmentsController(ICommercialRelationsService relations, BusinessClock clock,
    ILogger<CommercialAssignmentsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? kind = null, int parent = 0, int child = 0,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 100000 || kind is not (null or "SC" or "SS")) return BadRequest();
        try
        {
            var model = await relations.ReadAsync(page, cancellationToken);
            if (kind is not null)
            {
                var row = model.Relations.SingleOrDefault(x => x.Kind == kind && x.ParentUser == parent && x.ChildUser == child);
                if (row is null) return NotFound();
                var form = new CommercialRelationForm { ParentUser = row.ParentUser, ChildUser = row.ChildUser,
                    ExpectedRevision = row.Revision, Enabled = row.Enabled,
                    ValidThrough = row.ExpiresUtc is DateTime until ? clock.ToLocalDate(until.AddTicks(-1)) : null };
                if (kind == "SC") model.CustomerRelation = form; else model.SupervisorRelation = form;
            }
            return View(model);
        }
        catch (SqlException exception) { return Failure(exception); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SaveCustomer([Bind(Prefix = "CustomerRelation")] CommercialRelationForm model,
        CancellationToken cancellationToken) => Save("SC", "CustomerRelation", model, cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SaveSupervisor([Bind(Prefix = "SupervisorRelation")] CommercialRelationForm model,
        CancellationToken cancellationToken) => Save("SS", "SupervisorRelation", model, cancellationToken);

    private async Task<IActionResult> Save(string kind, string prefix, CommercialRelationForm form, CancellationToken ct)
    {
        if (form.ParentUser == form.ChildUser) ModelState.AddModelError(prefix + ".ChildUser", "Seleccioná dos personas distintas.");
        if (form.Enabled && form.ValidThrough is DateTime date && date.Date < clock.Today)
            ModelState.AddModelError(prefix + ".ValidThrough", "La vigencia no puede terminar en una fecha pasada.");
        if (form.Enabled && form.ValidThrough?.Date == DateTime.MaxValue.Date)
            ModelState.AddModelError(prefix + ".ValidThrough", "La fecha excede el rango permitido.");
        if (form.ExpectedRevision > 0 && (form.ParentUser <= 0 || form.ChildUser <= 0)) return BadRequest();
        if (ModelState.IsValid)
        {
            try
            {
                await relations.SaveAsync(kind, form, ct);
                TempData["SuccessMessage"] = form.Enabled ? "Relación comercial guardada." : "Relación comercial revocada. El historial se conserva.";
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException exception) when (exception.Number is 58702 or 58704 or 58705)
            {
                ModelState.AddModelError(prefix, exception.Number == 58704
                    ? "La relación existe o cambió. Abrila desde el listado para revisar su estado antes de guardar."
                    : "No se acreditó la elegibilidad o vigencia de la relación. Revisá las personas, sus permisos y la fecha.");
                Response.StatusCode = exception.Number == 58704 ? 409 : 400;
            }
            catch (SqlException exception) { return Failure(exception); }
        }
        try
        {
            var model = await relations.ReadAsync(1, ct);
            if (kind == "SC") model.CustomerRelation = form; else model.SupervisorRelation = form;
            return View("Index", model);
        }
        catch (SqlException exception) { return Failure(exception); }
    }

    private IActionResult Failure(SqlException exception)
    {
        if (exception.Number == 56802) return AuthorizationResults.AccessDenied();
        if (exception.Number == 56801) return Unauthorized();
        logger.LogWarning("Commercial relationship request failed. SQL {Code}; Trace {Trace}", exception.Number, HttpContext.TraceIdentifier);
        Response.StatusCode = 503; ViewData["TraceId"] = HttpContext.TraceIdentifier;
        return View("ServiceUnavailable");
    }
}
