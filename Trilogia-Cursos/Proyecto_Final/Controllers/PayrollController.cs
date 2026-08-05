using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-113/CU-114 — Cálculo de planilla, flujo de aprobación/pago y envío de boletas.
    [AdminAuthorize("Planilla", "PLANILLA_VER")]
    public class PayrollController : Controller
    {
        private readonly PayrollDbService _payroll;
        private readonly EmailService _emailService;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<PayrollController> _logger;

        public PayrollController(PayrollDbService payroll, EmailService emailService, AdminDbService adminDbService, ILogger<PayrollController> logger)
        {
            _payroll = payroll;
            _emailService = emailService;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var periodos = await _payroll.GetPeriodosAsync();
            return View(periodos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Planilla", "PLANILLA_GESTIONAR")]
        public async Task<IActionResult> CrearPeriodo(PayrollPeriodFormViewModel model)
        {
            if (!ModelState.IsValid || model.FechaFin < model.FechaInicio)
            {
                TempData["ErrorMessage"] = "Revisá el tipo de período y el rango de fechas.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var periodoId = await _payroll.CrearPeriodoAsync(model);
                await RegistrarAuditoriaAsync("Crear período de planilla", "Planilla",
                    $"Período #{periodoId} ({model.TipoPeriodo}) del {model.FechaInicio:d} al {model.FechaFin:d}.");
                TempData["SuccessMessage"] = "Período de planilla creado.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear período de planilla.");
                TempData["ErrorMessage"] = "No fue posible crear el período.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Detalle(int periodoId)
        {
            var periodos = await _payroll.GetPeriodosAsync();
            var periodo = periodos.FirstOrDefault(p => p.PeriodoPlanillaId == periodoId);
            if (periodo == null)
            {
                TempData["ErrorMessage"] = "No se encontró el período indicado.";
                return RedirectToAction(nameof(Index));
            }

            var model = new PayrollPeriodDetailViewModel
            {
                Periodo = periodo,
                Detalles = await _payroll.GetDetalleByPeriodoAsync(periodoId)
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Planilla", "PLANILLA_CALCULAR")]
        public async Task<IActionResult> Calcular(int periodoId)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Planilla";

            try
            {
                await _payroll.CalcularPeriodoAsync(periodoId, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Calcular planilla", "Planilla", $"Período #{periodoId} calculado.");
                TempData["SuccessMessage"] = "Planilla calculada correctamente.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular planilla.");
                TempData["ErrorMessage"] = "No fue posible calcular la planilla.";
            }

            return RedirectToAction(nameof(Detalle), new { periodoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Planilla", "PLANILLA_APROBAR")]
        public async Task<IActionResult> Aprobar(int periodoId)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Planilla";

            try
            {
                await _payroll.AprobarPeriodoAsync(periodoId, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Aprobar planilla", "Planilla", $"Período #{periodoId} aprobado.");
                TempData["SuccessMessage"] = "Planilla aprobada.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aprobar planilla.");
                TempData["ErrorMessage"] = "No fue posible aprobar la planilla.";
            }

            return RedirectToAction(nameof(Detalle), new { periodoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Planilla", "PLANILLA_PAGAR")]
        public async Task<IActionResult> MarcarPagado(int periodoId)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Planilla";

            try
            {
                await _payroll.MarcarPagadoAsync(periodoId, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Marcar planilla pagada", "Planilla", $"Período #{periodoId} pagado.");
                TempData["SuccessMessage"] = "Planilla marcada como pagada.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar planilla pagada.");
                TempData["ErrorMessage"] = "No fue posible registrar el pago.";
            }

            return RedirectToAction(nameof(Detalle), new { periodoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Planilla", "PLANILLA_REVERTIR")]
        public async Task<IActionResult> Revertir(PayrollRevertViewModel model)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Planilla";

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Debe indicar el motivo de la reversión.";
                return RedirectToAction(nameof(Detalle), new { periodoId = model.PeriodoPlanillaId });
            }

            try
            {
                await _payroll.RevertirPeriodoAsync(model.PeriodoPlanillaId, usuarioId, usuarioNombre, model.MotivoReversion);
                await RegistrarAuditoriaAsync("Revertir planilla", "Planilla",
                    $"Período #{model.PeriodoPlanillaId} revertido. Motivo: {model.MotivoReversion}");
                TempData["SuccessMessage"] = "Planilla revertida a estado Abierto.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al revertir planilla.");
                TempData["ErrorMessage"] = "No fue posible revertir la planilla.";
            }

            return RedirectToAction(nameof(Detalle), new { periodoId = model.PeriodoPlanillaId });
        }

        // CU-114 — envío individual de boleta por correo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Planilla", "PLANILLA_BOLETAS_GESTIONAR")]
        public async Task<IActionResult> EnviarBoleta(int planillaDetalleId, int periodoId)
        {
            var recibo = await _payroll.ObtenerDetalleEmpleadoAsync(planillaDetalleId);
            if (recibo == null)
            {
                TempData["ErrorMessage"] = "No se encontró la boleta indicada.";
                return RedirectToAction(nameof(Detalle), new { periodoId });
            }

            var asunto = $"Boleta de pago — período {recibo.FechaInicio:d} al {recibo.FechaFin:d}";
            var contenido = ConstruirHtmlBoleta(recibo);

            try
            {
                _emailService.SendEmail(recibo.Correo, asunto, contenido);
                await _payroll.RegistrarEnvioComprobanteAsync(planillaDetalleId, recibo.Correo, true, null);
                await RegistrarAuditoriaAsync("Enviar boleta", "Planilla", $"Boleta #{planillaDetalleId} enviada a {recibo.Correo}.");
                TempData["SuccessMessage"] = $"Boleta enviada a {recibo.Correo}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar boleta por correo.");
                await _payroll.RegistrarEnvioComprobanteAsync(planillaDetalleId, recibo.Correo, false, ex.Message);
                TempData["ErrorMessage"] = "No fue posible enviar la boleta por correo. Se registró el intento fallido.";
            }

            return RedirectToAction(nameof(Detalle), new { periodoId });
        }

        private static string ConstruirHtmlBoleta(PayrollReceiptViewModel r)
        {
            return $@"
                <h2>Boleta de pago</h2>
                <p><strong>{r.NombreCompleto}</strong></p>
                <p>Período: {r.FechaInicio:d} al {r.FechaFin:d} ({r.TipoPeriodo})</p>
                <table border='1' cellpadding='6' cellspacing='0'>
                    <tr><td>Salario base</td><td>₡{r.SalarioBase:N2}</td></tr>
                    <tr><td>Horas extra ({r.HorasExtraPagadas}h)</td><td>₡{r.MontoHorasExtra:N2}</td></tr>
                    <tr><td>Comisiones</td><td>₡{r.MontoComisiones:N2}</td></tr>
                    <tr><td><strong>Salario bruto</strong></td><td><strong>₡{r.SalarioBruto:N2}</strong></td></tr>
                    <tr><td>Deducción CCSS</td><td>-₡{r.DeduccionCcss:N2}</td></tr>
                    <tr><td>Deducción renta</td><td>-₡{r.DeduccionRenta:N2}</td></tr>
                    <tr><td><strong>Salario neto</strong></td><td><strong>₡{r.SalarioNeto:N2}</strong></td></tr>
                </table>";
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
