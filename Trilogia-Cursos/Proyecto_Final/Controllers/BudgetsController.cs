using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[AdminAuthorize("Presupuestos", "PRESUPUESTOS_VER")]
public sealed class BudgetsController : Controller
{
    private readonly BudgetDbService _budgets;
    private readonly AdminDbService _admin;
    private readonly ILogger<BudgetsController> _logger;

    public BudgetsController(BudgetDbService budgets, AdminDbService admin, ILogger<BudgetsController> logger)
    { _budgets = budgets; _admin = admin; _logger = logger; }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] BudgetFilterViewModel filter)
    {
        var options = await _budgets.GetOptionsAsync();
        return View(new BudgetsIndexViewModel
        {
            Filter = filter, Budgets = await _budgets.ListAsync(filter), Departments = options.Departments,
            Categories = options.Categories, NewBudget = new() { Year = filter.Year ?? DateTime.Today.Year }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var model = await _budgets.GetDetailsAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_GESTIONAR")]
    public async Task<IActionResult> Create(BudgetCreateViewModel model)
    {
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = "Revise los datos del presupuesto."; return RedirectToAction(nameof(Index), new { year = model.Year }); }
        try
        {
            var id = await _budgets.CreateAsync(model, UserId(), UserName());
            await AuditAsync("CREAR_PRESUPUESTO", $"Presupuesto #{id}, año {model.Year}, departamento #{model.DepartmentId}, monto {model.AnnualAmount:N2}.");
            TempData["SuccessMessage"] = "Presupuesto anual creado en borrador.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception exception) { Handle(exception, "crear el presupuesto"); return RedirectToAction(nameof(Index), new { year = model.Year }); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_GESTIONAR")]
    public async Task<IActionResult> SaveDetail(BudgetDetailEditViewModel model)
    {
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = "Revise la línea presupuestaria."; return RedirectToAction(nameof(Details), new { id = model.BudgetId }); }
        try
        {
            await _budgets.SaveDetailAsync(model, UserId(), UserName());
            await AuditAsync("EDITAR_DETALLE_PRESUPUESTO", $"Presupuesto #{model.BudgetId}, categoría #{model.CategoryId}, mes {model.Month}.");
            TempData["SuccessMessage"] = "Línea presupuestaria guardada.";
        }
        catch (Exception exception) { Handle(exception, "guardar la línea presupuestaria"); }
        return RedirectToAction(nameof(Details), new { id = model.BudgetId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_GESTIONAR")]
    public async Task<IActionResult> UpdateDraft(BudgetHeaderEditViewModel model)
    {
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = "Revise el encabezado del presupuesto."; return RedirectToAction(nameof(Details), new { id = model.BudgetId }); }
        try
        {
            await _budgets.UpdateDraftAsync(model, UserId(), UserName());
            await AuditAsync("EDITAR_PRESUPUESTO", $"Presupuesto #{model.BudgetId}, año {model.Year}, monto {model.AnnualAmount:N2}.");
            TempData["SuccessMessage"] = "Encabezado actualizado; verifique o redistribuya el detalle antes de presentar.";
        }
        catch (Exception exception) { Handle(exception, "actualizar el presupuesto"); }
        return RedirectToAction(nameof(Details), new { id = model.BudgetId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_GESTIONAR")]
    public async Task<IActionResult> Distribute(int id, int categoryId)
    {
        try
        {
            await _budgets.DistributeAsync(id, categoryId, UserId(), UserName());
            await AuditAsync("DISTRIBUIR_PRESUPUESTO", $"Presupuesto #{id}, categoría #{categoryId}, distribución anual exacta.");
            TempData["SuccessMessage"] = "Monto distribuido en los 12 meses; el ajuste de centavos se aplicó al último mes.";
        }
        catch (Exception exception) { Handle(exception, "distribuir el presupuesto"); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_GESTIONAR")]
    public Task<IActionResult> Submit(int id) => Transition(id, "Presentar", null, "Presupuesto presentado para aprobación.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_APROBAR")]
    public Task<IActionResult> Approve(int id) => Transition(id, "Aprobar", null, "Presupuesto aprobado y bloqueado para edición.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_APROBAR")]
    public Task<IActionResult> Reject(int id, string reason) => Transition(id, "Rechazar", reason, "Presupuesto rechazado.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_CERRAR")]
    public Task<IActionResult> Close(int id) => Transition(id, "Cerrar", null, "Presupuesto cerrado.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("finance-write")]
    [AdminAuthorize("Presupuestos", "PRESUPUESTOS_GESTIONAR")]
    public async Task<IActionResult> Copy(int id, int targetYear)
    {
        try
        {
            var copyId = await _budgets.CopyAsync(id, targetYear, UserId(), UserName());
            await AuditAsync("COPIAR_PRESUPUESTO", $"Presupuesto #{id} copiado como #{copyId} para {targetYear}.");
            TempData["SuccessMessage"] = "Presupuesto copiado como nuevo borrador.";
            return RedirectToAction(nameof(Details), new { id = copyId });
        }
        catch (Exception exception) { Handle(exception, "copiar el presupuesto"); return RedirectToAction(nameof(Details), new { id }); }
    }

    private async Task<IActionResult> Transition(int id, string action, string? reason, string success)
    {
        try
        {
            await _budgets.TransitionAsync(id, action, reason, UserId(), UserName());
            await AuditAsync($"{action.ToUpperInvariant()}_PRESUPUESTO", $"Presupuesto #{id}. Motivo: {reason ?? "N/A"}.");
            TempData["SuccessMessage"] = success;
        }
        catch (Exception exception) { Handle(exception, $"{action.ToLowerInvariant()} el presupuesto"); }
        return RedirectToAction(nameof(Details), new { id });
    }

    private int UserId() => HttpContext.Session.GetInt32("UserId") ?? 0;
    private string UserName() => HttpContext.Session.GetString("UserFullName") ?? "Usuario";
    private Task AuditAsync(string action, string detail) => _admin.CreateAuditLogAsync(UserId(), UserName(), HttpContext.Session.GetString("UserEmail"),
        HttpContext.Session.GetString("UserRole"), action, "Presupuestos", detail, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    private void Handle(Exception exception, string operation)
    {
        if (exception is SqlException sql && sql.Number >= 50000) _logger.LogWarning(exception, "Regla de negocio al {Operation}.", operation);
        else _logger.LogError(exception, "Error al {Operation}.", operation);
        TempData["ErrorMessage"] = $"No fue posible {operation}.";
    }
}
