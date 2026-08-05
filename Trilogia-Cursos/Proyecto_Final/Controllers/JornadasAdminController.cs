using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-112 — Aprobación de jornadas (horas, extras y ausencias) por parte del supervisor.
    [AdminAuthorize("RRHH", "RRHH_JORNADAS_APROBAR")]
    public class JornadasAdminController : Controller
    {
        private readonly JornadasDbService _jornadas;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<JornadasAdminController> _logger;

        public JornadasAdminController(JornadasDbService jornadas, AdminDbService adminDbService, ILogger<JornadasAdminController> logger)
        {
            _jornadas = jornadas;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta)
        {
            var model = new JornadasAdminViewModel
            {
                Desde = desde ?? DateTime.Today.AddDays(-14),
                Hasta = hasta ?? DateTime.Today
            };
            model.Pendientes = await _jornadas.GetJornadasPendientesAsync(model.Desde, model.Hasta);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolver(JornadaDecisionViewModel model, DateTime desde, DateTime hasta)
        {
            var supervisorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var supervisorNombre = HttpContext.Session.GetString("UserFullName") ?? "Supervisor";

            try
            {
                await _jornadas.ResolverJornadaAsync(supervisorId, supervisorNombre, model);
                await RegistrarAuditoriaAsync(
                    model.Decision == "Aprobada" ? "Aprobar jornada" : "Rechazar jornada",
                    "RRHH",
                    $"Jornada #{model.JornadaId} marcada como {model.Decision}.");
                TempData["SuccessMessage"] = $"Jornada {model.Decision.ToLower()} correctamente.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al resolver jornada.");
                TempData["ErrorMessage"] = "No fue posible procesar la jornada.";
            }

            return RedirectToAction(nameof(Index), new { desde, hasta });
        }

        private async Task RegistrarAuditoriaAsync(string accion, string modulo, string descripcion)
        {
            await _adminDbService.CreateAuditLogAsync(
                HttpContext.Session.GetInt32("UserId"),
                HttpContext.Session.GetString("UserFullName"),
                HttpContext.Session.GetString("UserEmail"),
                HttpContext.Session.GetString("UserRole"),
                accion,
                modulo,
                descripcion,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }
    }
}
