using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;
using Proyecto_Final.Validation;

namespace Proyecto_Final.Controllers
{
    // CU-162/163/164 — Comodatos: asignación, devolución/retiro y consulta de rentabilidad.
    // Gestión reutiliza el permiso ACTIVOS_GESTIONAR (módulo Activos).
    [AdminAuthorize("Activos", "ACTIVOS_GESTIONAR")]
    public class ComodatosController : Controller
    {
        private readonly FleetDbService _fleet;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<ComodatosController> _logger;

        public ComodatosController(FleetDbService fleet, AdminDbService adminDbService, ILogger<ComodatosController> logger)
        {
            _fleet = fleet;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? estado, string? buscar)
        {
            ViewBag.Estado = estado;
            ViewBag.Buscar = buscar;
            var comodatos = await _fleet.GetComodatosAsync(estado, buscar);
            return View(comodatos);
        }

        // CU-162 — Asignar
        [HttpGet]
        public async Task<IActionResult> Assign()
        {
            var model = new ComodatoAssignViewModel { FechaAsignacion = DateTime.Today };
            await CargarOpcionesAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(ComodatoAssignViewModel model)
        {
            model.ClienteIdentificacion = CostaRicanIdentificationAttribute.Normalize(model.ClienteIdentificacion);
            await CargarOpcionesAsync(model);

            // BUG-16-02: el cliente sale de la lista de clientes registrados y activos.
            var cliente = model.Clientes.FirstOrDefault(c => c.UsuarioId == model.ClienteUsuarioId);
            if (model.ClienteUsuarioId.HasValue && cliente is null)
                ModelState.AddModelError(nameof(model.ClienteUsuarioId), "Seleccione un cliente registrado y activo.");
            if (!ModelState.IsValid || cliente is null)
                return View(model);
            model.ClienteNombre = cliente.NombreCompleto;

            // BUG-16-02 · CU-162-03: con saldo vencido se alerta el riesgo y se bloquea.
            var deuda = await _fleet.GetComodatoClientDebtAsync(cliente.UsuarioId);
            if (deuda.EsMoroso)
            {
                deuda.ClienteNombre = cliente.NombreCompleto;
                model.Riesgo = deuda;
                await RegistrarAuditoriaAsync("Comodato bloqueado por morosidad", "Activos",
                    $"Activo #{model.ActivoId} no asignado a {cliente.NombreCompleto}: saldo vencido ₡{deuda.SaldoVencido:N2} ({deuda.DiasAtraso} días).");
                return View(model);
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";
            try
            {
                var id = await _fleet.AssignComodatoAsync(model, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Asignar comodato", "Activos",
                    $"Comodato #{id}: activo #{model.ActivoId} asignado a {model.ClienteNombre}.");
                TempData["SuccessMessage"] = "Comodato registrado correctamente. Se emitió el contrato de comodato.";
                TempData["ContratoComodatoId"] = id;
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (ex.Number == 53086)
            {
                // La base también bloquea por morosidad (0245), p. ej. si la deuda venció entre la consulta y el registro.
                _logger.LogWarning(ex, "Comodato bloqueado por morosidad del cliente {ClienteUsuarioId}.", cliente.UsuarioId);
                ModelState.AddModelError(string.Empty, "El cliente tiene saldo vencido: la asignación queda bloqueada hasta que salde su cuenta.");
                return View(model);
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                ModelState.AddModelError(string.Empty, "No fue posible completar la operación solicitada.");
                model.ActivosDisponibles = await _fleet.GetAvailableAssetsAsync();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar comodato.");
                ModelState.AddModelError(string.Empty, "No fue posible registrar el comodato.");
                model.ActivosDisponibles = await _fleet.GetAvailableAssetsAsync();
                return View(model);
            }
        }

        private async Task CargarOpcionesAsync(ComodatoAssignViewModel model)
        {
            model.ActivosDisponibles = await _fleet.GetAvailableAssetsAsync();
            model.Clientes = await _fleet.GetComodatoClientsAsync();
        }

        // BUG-16-01 · CU-162-01 — Contrato de comodato en PDF.
        [HttpGet]
        public async Task<IActionResult> Contract(int id)
        {
            if (id <= 0) return NotFound();
            try
            {
                var contrato = await _fleet.GetComodatoContractAsync(id);
                if (contrato is null) return NotFound();
                return File(ComodatoContractPdf.Build(contrato), "application/pdf", ComodatoContractPdf.FileName(contrato));
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "No se pudo emitir el contrato del comodato {ComodatoId}.", id);
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        // CU-163 — Devolver / Retirar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(ComodatoReturnViewModel model)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";
            try
            {
                await _fleet.ReturnComodatoAsync(model, usuarioId, usuarioNombre);
                var destino = model.Destino == "Mantenimiento" ? "enviado a mantenimiento" : "reingresado al inventario";
                await RegistrarAuditoriaAsync("Devolver comodato", "Activos",
                    $"Comodato #{model.ComodatoId} cerrado; equipo {destino}.");
                // BUG-16-03: con avería, sp_Comodato_Devolver avisa a taller y cobros (0243).
                TempData["SuccessMessage"] = model.Destino == "Mantenimiento"
                    ? $"Comodato cerrado. Equipo {destino} (en reparación). Se envió una alerta a taller y cobros."
                    : $"Comodato cerrado. Equipo {destino}.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al devolver comodato.");
                TempData["ErrorMessage"] = "No fue posible registrar la devolución.";
            }
            return RedirectToAction(nameof(Index));
        }

        // CU-164 — Rentabilidad (consulta). Auditor/gerente por COMODATOS_CONSULTAR.
        [HttpGet]
        [AdminAuthorize("Activos", "COMODATOS_CONSULTAR")]
        public async Task<IActionResult> Profitability()
        {
            // BUG-16-04: al consultar se generan los avisos de baja rentabilidad
            // (30 días sin compras) y cada aviso nuevo queda en la bitácora.
            // Si falla, el reporte se muestra igual.
            try
            {
                foreach (var alerta in await _fleet.GenerateComodatoProfitabilityAlertsAsync())
                {
                    await RegistrarAuditoriaAsync("Alerta de baja rentabilidad", "Activos",
                        $"Comodato #{alerta.ComodatoId}: {alerta.ClienteNombre} no compra hace {alerta.DiasSinCompra} días; se sugiere retirar el equipo {alerta.CodigoActivo}.");
                }
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "No se pudieron generar las alertas de baja rentabilidad de comodatos.");
            }

            var datos = await _fleet.GetComodatoProfitabilityAsync();
            return View(datos);
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
