using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[AdminAuthorize("Documentos", "DOCUMENTOS_DELEGAR", allowAdministratorBypass: false)]
[DocumentAccessExceptionFilter]
public sealed class DocumentDelegationsController(IDocumentDelegationService grants, BusinessClock clock,
    ILogger<DocumentDelegationsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int department = 0, int grantee = 0, string? operation = null,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 100000 || (operation is not null && !DocumentOperations.Labels.ContainsKey(operation))) return BadRequest();
        try
        {
            var model = await grants.ReadAsync(page, cancellationToken);
            if (operation is not null)
            {
                var row = model.Grants.SingleOrDefault(x => x.Department == department && x.Grantee == grantee && x.Operation == operation);
                if (row is null) return NotFound();
                model.Form = new() { Department = row.Department, Grantee = row.Grantee, Operation = row.Operation,
                    Enabled = row.Enabled, ExpectedRevision = row.Revision, ValidThrough = clock.ToLocalDate(row.ExpiresUtc.AddTicks(-1)) };
            }
            return View(model);
        }
        catch (SqlException exception) { return Failure(exception); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([Bind(Prefix = "Form")] DocumentDelegationForm form, CancellationToken cancellationToken)
    {
        if ((string.IsNullOrEmpty(form.Operation) || !DocumentOperations.Labels.ContainsKey(form.Operation))) ModelState.AddModelError("Form.Operation", "Seleccioná una operación disponible.");
        if (form.Enabled && (form.ValidThrough is null || form.ValidThrough.Value.Date < clock.Today || form.ValidThrough.Value.Date == DateTime.MaxValue.Date))
            ModelState.AddModelError("Form.ValidThrough", "Indicá el último día de vigencia, desde hoy.");
        if (ModelState.IsValid)
        {
            try
            {
                await grants.SaveAsync(form, cancellationToken);
                TempData["SuccessMessage"] = form.Enabled ? "Autorización departamental guardada." : "Autorización revocada. La auditoría se conserva.";
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException exception) when (exception.Number is 57504 or 57505)
            {
                Response.StatusCode = exception.Number == 57504 ? 409 : 400;
                ModelState.AddModelError("Form", exception.Number == 57504 ? "La autorización cambió. Abrila desde el listado antes de guardar."
                    : "No se acreditó la persona, el departamento o la vigencia. No podés autorizarte a vos mismo.");
            }
            catch (SqlException exception) { return Failure(exception); }
        }
        try { var model = await grants.ReadAsync(1, cancellationToken); model.Form = form; return View("Index", model); }
        catch (SqlException exception) { return Failure(exception); }
    }
    private IActionResult Failure(SqlException exception)
    {
        if (exception.Number == 56801) return Unauthorized();
        if (exception.Number == 56802) return AuthorizationResults.AccessDenied();
        logger.LogWarning("Document delegation request failed. SQL {Code}; Trace {Trace}", exception.Number, HttpContext.TraceIdentifier);
        Response.StatusCode = 503; ViewData["TraceId"] = HttpContext.TraceIdentifier; return View("ServiceUnavailable");
    }
}
