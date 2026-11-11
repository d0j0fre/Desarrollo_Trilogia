using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[AdminAuthorize("RRHH", "RRHH_JORNADAS_APROBAR")]
public sealed class AttendanceController : Controller
{
    private readonly IAttendanceService _attendance;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(IAttendanceService attendance, ILogger<AttendanceController> logger)
    {
        _attendance = attendance; _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, CancellationToken cancellationToken)
    {
        var end = (hasta ?? DateTime.Today).Date;
        var start = (desde ?? end.AddDays(-30)).Date;
        if (start > end) (start, end) = (end, start);
        ViewBag.Desde = start; ViewBag.Hasta = end;
        // G2-26: la propia jornada no se ofrece para aprobarla (la aprueba otra persona).
        var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
        var pending = await _attendance.GetPendingAsync(start, end, cancellationToken);
        ViewBag.PropiasOcultas = pending.Count(entry => entry.UsuarioId == userId && userId > 0);
        return View(pending.Where(entry => userId <= 0 || entry.UsuarioId != userId).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(AttendanceDecisionViewModel model, DateTime? desde, DateTime? hasta, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = "La decisión no es válida."; return RedirectToAction(nameof(Index), new { desde, hasta }); }
        try
        {
            await _attendance.DecideAsync(model, HttpContext.Session.GetInt32("UserId") ?? 0,
                HttpContext.Session.GetString("UserFullName") ?? "Supervisor", cancellationToken);
            TempData["SuccessMessage"] = "Jornada actualizada correctamente.";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No fue posible resolver la jornada {AttendanceId}.", model.JornadaId);
            // G2-26: la causa («No puede resolver su propia jornada.») viene del procedimiento.
            TempData["ErrorMessage"] = BusinessErrors.MessageOr(exception, "No fue posible resolver la jornada; recargue la lista.");
        }
        return RedirectToAction(nameof(Index), new { desde, hasta });
    }
}
