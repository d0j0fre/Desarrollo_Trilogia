using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[SessionAuthorize("Empleado", "Vendedor")]
public sealed class MyAttendanceController : Controller
{
    private readonly IAttendanceService _attendance;
    private readonly ILogger<MyAttendanceController> _logger;

    public MyAttendanceController(IAttendanceService attendance, ILogger<MyAttendanceController> logger)
    {
        _attendance = attendance; _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, long? editar, CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
        var end = (hasta ?? DateTime.Today).Date; var start = (desde ?? end.AddDays(-30)).Date;
        var jornadas = await _attendance.GetMineAsync(userId, start, end, cancellationToken);
        // «Completar» un borrador o una jornada rechazada carga sus datos en el formulario.
        var editable = jornadas.FirstOrDefault(j => j.JornadaId == editar && AttendanceRules.IsEditable(j.Estado));
        return View(new MyAttendanceIndexViewModel
        {
            Form = editable is null ? new AttendanceFormViewModel { Fecha = DateTime.Today } : AttendanceRules.ToForm(editable),
            Jornadas = jornadas
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AttendanceFormViewModel model, bool submit, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)); return RedirectToAction(nameof(Index)); }
        var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
        try
        {
            if (model.JornadaId <= 0)
            {
                // Una sola jornada por fecha: si ya hay una para esa fecha, se completa esa en vez de crear otra.
                var existing = (await _attendance.GetMineAsync(userId, model.Fecha.Date, model.Fecha.Date, cancellationToken)).FirstOrDefault();
                if (existing is not null)
                {
                    if (!AttendanceRules.IsEditable(existing.Estado))
                    {
                        TempData["ErrorMessage"] = $"Ya registró la jornada del {existing.Fecha:dd/MM/yyyy} y está {existing.Estado.ToLowerInvariant()}: no se puede modificar.";
                        return RedirectToAction(nameof(Index));
                    }

                    model.JornadaId = existing.JornadaId;
                    model.RowVersionBase64 = existing.RowVersionBase64;
                }
            }

            await _attendance.SaveMineAsync(userId, model, submit, cancellationToken);
            TempData["SuccessMessage"] = submit ? "Jornada enviada para aprobación." : "Borrador de jornada guardado.";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No fue posible guardar la jornada del usuario actual.");
            TempData["ErrorMessage"] = "No fue posible guardar la jornada.";
        }
        return RedirectToAction(nameof(Index));
    }
}
