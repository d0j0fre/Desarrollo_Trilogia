using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-211/213 — Metas mensuales por vendedor y reporte de cumplimiento de KPIs.
    [AdminAuthorize("Metas y KPIs", "METAS_GESTIONAR")]
    public class KpisController : Controller
    {
        private readonly KpiDbService _kpis;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<KpisController> _logger;

        public KpisController(KpiDbService kpis, AdminDbService adminDbService, ILogger<KpisController> logger)
        {
            _kpis = kpis;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        // CU-211 — Gestión de metas
        [HttpGet]
        public async Task<IActionResult> Index(int? anio, int? mes)
        {
            var (a, m) = NormalizarPeriodo(anio, mes);
            var model = new MetasIndexViewModel
            {
                Anio = a,
                Mes = m,
                Metas = await _kpis.GetMetasAsync(a, m),
                Vendedores = await _kpis.GetSellerOptionsAsync(),
                Nueva = new MetaFormViewModel { Anio = a, Mes = m }
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(MetaFormViewModel nueva)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revisá los datos de la meta (vendedor, período y monto).";
                return RedirectToAction(nameof(Index), new { anio = nueva.Anio, mes = nueva.Mes });
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Gerente";
            try
            {
                await _kpis.UpsertMetaAsync(nueva, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync(nueva.Reemplazar ? "Reemplazar meta de ventas" : "Definir meta de ventas", "Metas y KPIs",
                    $"Meta de ₡{nueva.MontoMeta:N2} para vendedor #{nueva.VendedorUsuarioId} en {nueva.Mes:00}/{nueva.Anio}" +
                    (nueva.Reemplazar ? " (reemplaza la meta existente, confirmado)." : "."));
                TempData["SuccessMessage"] = nueva.Reemplazar ? "Meta reemplazada correctamente." : "Meta guardada correctamente.";
            }
            catch (Exception ex)
            {
                // Las reglas de negocio del procedimiento («Ya existe una meta…», vendedor inválido…) se muestran tal cual.
                if (BusinessErrors.TryGetMessage(ex, out _))
                    _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                else
                    _logger.LogError(ex, "Error al guardar meta.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible guardar la meta.");
            }
            return RedirectToAction(nameof(Index), new { anio = nueva.Anio, mes = nueva.Mes });
        }

        // CU-213 — Reporte de cumplimiento global de KPIs
        [HttpGet]
        [AdminAuthorize("Metas y KPIs", "REPORTE_KPI")]
        public async Task<IActionResult> Report(int? anio, int? mes)
        {
            var (a, m) = NormalizarPeriodo(anio, mes);
            var model = await _kpis.GetGlobalKpiAsync(a, m);
            return View(model);
        }

        private static (int anio, int mes) NormalizarPeriodo(int? anio, int? mes)
        {
            var hoy = DateTime.Now;
            var a = anio is >= 2000 and <= 2100 ? anio.Value : hoy.Year;
            var m = mes is >= 1 and <= 12 ? mes.Value : hoy.Month;
            return (a, m);
        }

        private async Task RegistrarAuditoriaAsync(string accion, string modulo, string descripcion)
        {
            await _adminDbService.CreateAuditLogAsync(
                HttpContext.Session.GetInt32("UserId"),
                HttpContext.Session.GetString("UserFullName"),
                HttpContext.Session.GetString("UserEmail"),
                HttpContext.Session.GetString("UserRole"),
                accion, modulo, descripcion,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }
    }
}
