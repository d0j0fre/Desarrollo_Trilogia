using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-152 (kilometraje), CU-153 (mantenimiento), CU-154 (alertas y documentos).
    public class FleetController : Controller
    {
        private readonly FleetDbService _fleet;
        private readonly LogisticsDbService _logistics;
        private readonly AdminDbService _adminDbService;
        private readonly IRolePermissionService _permissionService;
        private readonly ILogger<FleetController> _logger;

        public FleetController(FleetDbService fleet, LogisticsDbService logistics, AdminDbService adminDbService, IRolePermissionService permissionService, ILogger<FleetController> logger)
        {
            _fleet = fleet;
            _logistics = logistics;
            _adminDbService = adminDbService;
            _permissionService = permissionService;
            _logger = logger;
        }

        // ── CU-152 Kilometraje (chofer + admin) ─────────────
        [HttpGet]
        [AdminAuthorize("Flota", "FLOTA_KILOMETRAJE")]
        public async Task<IActionResult> Mileage()
        {
            await PopulateMileageVehiclesAsync();
            // Quien no administra jornadas ajenas (FLOTA_KILOMETRAJE_ADMIN o Administrador) solo ve las suyas.
            int? choferFiltro = await CanAdministerMileageAsync() ? null : HttpContext.Session.GetInt32("UserId");
            var vm = new MileageIndexViewModel
            {
                Jornadas = await _fleet.GetMileageAsync(null, choferFiltro)
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Flota", "FLOTA_KILOMETRAJE")]
        public async Task<IActionResult> OpenMileage(MileageOpenViewModel nueva)
        {
            var choferId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var choferNombre = HttpContext.Session.GetString("UserFullName") ?? "Chofer";

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revise los datos del kilometraje inicial.";
                return RedirectToAction(nameof(Mileage));
            }
            try
            {
                await _fleet.OpenMileageAsync(nueva, choferId, choferNombre);
                TempData["SuccessMessage"] = "Jornada iniciada. Kilometraje inicial registrado.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                // G3-08: el motivo exacto («El kilometraje inicial… no puede ser menor al odómetro actual…»).
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al abrir kilometraje.");
                TempData["ErrorMessage"] = "No fue posible registrar el kilometraje inicial.";
            }
            return RedirectToAction(nameof(Mileage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Flota", "FLOTA_KILOMETRAJE")]
        public async Task<IActionResult> CloseMileage(int kilometrajeId, int kmFinal)
        {
            // Propiedad: el procedimiento cierra solo la jornada del actor, salvo que pueda administrar las ajenas.
            var actorUsuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (actorUsuarioId <= 0)
            {
                return AuthorizationResults.AccessDenied();
            }

            try
            {
                await _fleet.CloseMileageAsync(kilometrajeId, kmFinal, actorUsuarioId, await CanAdministerMileageAsync());
                TempData["SuccessMessage"] = "Jornada cerrada. Kilometraje final registrado.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar kilometraje.");
                TempData["ErrorMessage"] = "No fue posible registrar el kilometraje final.";
            }
            return RedirectToAction(nameof(Mileage));
        }

        // ── CU-153 Mantenimiento ────────────────────────────
        [HttpGet]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> Maintenance(string? estado)
        {
            await PopulateVehiclesAsync();
            var vm = new MaintenanceIndexViewModel
            {
                Estado = estado,
                Ordenes = await _fleet.GetMaintenanceAsync(null, estado)
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> CreateMaintenance(MaintenanceFormViewModel nueva)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            // G2-18: una orden Completada lleva su fecha de realización (la del día si no se indica) y no puede ser futura.
            if (nueva.FechaRealizada.HasValue && nueva.FechaRealizada.Value.Date > DateTime.Today)
                ModelState.AddModelError(nameof(nueva.FechaRealizada), "La fecha de realización no puede ser futura.");
            if (nueva.Estado == "Completada" && !nueva.FechaRealizada.HasValue)
                nueva.FechaRealizada = DateTime.Today;

            if (!ModelState.IsValid)
            {
                return await MaintenanceViewWithFormAsync(nueva);
            }
            try
            {
                var (_, fueraDeCirculacion) = await _fleet.CreateMaintenanceAsync(nueva, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Registrar mantenimiento", "Flota",
                    $"Orden {nueva.Tipo} ({nueva.Estado}) registrada para vehículo #{nueva.VehiculoId}."
                    + (fueraDeCirculacion ? " El vehículo quedó fuera de circulación." : string.Empty));
                TempData["SuccessMessage"] = fueraDeCirculacion
                    ? "Orden de mantenimiento registrada. El vehículo quedó fuera de circulación hasta que se complete la reparación."
                    : "Orden de mantenimiento registrada.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar la orden de mantenimiento.");
                ModelState.AddModelError(string.Empty, BusinessErrors.MessageOr(ex, "No fue posible registrar la orden de mantenimiento."));
                return await MaintenanceViewWithFormAsync(nueva);
            }
            return RedirectToAction(nameof(Maintenance));
        }

        // BUG-15-01: Programada pasa a En proceso (una correctiva saca el vehículo de circulación).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> StartMaintenance(int ordenId)
        {
            try
            {
                await _fleet.StartMaintenanceAsync(ordenId);
                await RegistrarAuditoriaAsync("Iniciar mantenimiento", "Flota", $"Orden de mantenimiento #{ordenId} pasó a En proceso.");
                TempData["SuccessMessage"] = "Orden en proceso. Si es correctiva, el vehículo quedó fuera de circulación.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo iniciar la orden de mantenimiento {OrdenId}.", ordenId);
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible iniciar la orden.");
            }
            return RedirectToAction(nameof(Maintenance));
        }

        // G2-18: al completar se pide el costo final y la fecha de realización.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> CompleteMaintenance(int ordenId, decimal? costo, DateTime? fechaRealizada)
        {
            var fecha = (fechaRealizada ?? DateTime.Today).Date;
            if (fecha > DateTime.Today)
            {
                TempData["ErrorMessage"] = "La fecha de realización no puede ser futura.";
                return RedirectToAction(nameof(Maintenance));
            }
            try
            {
                await _fleet.CompleteMaintenanceAsync(ordenId, fecha, costo);
                await RegistrarAuditoriaAsync("Completar mantenimiento", "Flota",
                    $"Orden de mantenimiento #{ordenId} marcada como completada el {fecha:dd/MM/yyyy}" + (costo.HasValue ? $" con costo final ₡{costo.Value:N2}." : "."));
                TempData["SuccessMessage"] = "Orden marcada como completada. Si era una reparación, el vehículo volvió a circulación.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo completar la orden de mantenimiento {OrdenId}.", ordenId);
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la orden.");
            }
            return RedirectToAction(nameof(Maintenance));
        }

        // ── CU-154 Alertas y documentos ─────────────────────
        [HttpGet]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> Alerts(int diasAviso = 15, int? vehiculoId = null)
        {
            await PopulateVehiclesAsync();
            diasAviso = Math.Clamp(diasAviso, 1, 180);
            var vm = new FleetAlertsViewModel
            {
                DiasAviso = diasAviso,
                VehiculoId = vehiculoId,
                Alertas = await _fleet.GetAlertsAsync(diasAviso),
                Documentos = await _fleet.GetDocumentsAsync(vehiculoId)
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> CreateDocument(VehicleDocumentFormViewModel nuevoDocumento)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revise los datos del documento.";
                return RedirectToAction(nameof(Alerts));
            }
            try
            {
                await _fleet.CreateDocumentAsync(nuevoDocumento);
                await RegistrarAuditoriaAsync("Registrar documento", "Flota",
                    $"Documento {nuevoDocumento.Tipo} para vehículo #{nuevoDocumento.VehiculoId} (vence {nuevoDocumento.FechaVencimiento:dd/MM/yyyy}).");
                TempData["SuccessMessage"] = "Documento registrado. Se vigilará su vencimiento y reemplaza como vigente a los anteriores del mismo tipo.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar el documento del vehículo.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible registrar el documento.");
            }
            return RedirectToAction(nameof(Alerts));
        }

        // G2-17: corregir un documento (por ejemplo, una fecha mal digitada).
        [HttpGet]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> EditDocument(int id)
        {
            var model = await _fleet.GetDocumentByIdAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "No se encontró el documento solicitado.";
                return RedirectToAction(nameof(Alerts));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> EditDocument(VehicleDocumentEditViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                await _fleet.UpdateDocumentAsync(model);
                await RegistrarAuditoriaAsync("Editar documento", "Flota",
                    $"Documento #{model.DocumentoId} ({model.Tipo}) del vehículo {model.VehiculoPlaca}: vence {model.FechaVencimiento:dd/MM/yyyy}.");
                TempData["SuccessMessage"] = "Documento actualizado.";
                return RedirectToAction(nameof(Alerts), new { vehiculoId = model.VehiculoId });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo actualizar el documento {DocumentoId}.", model.DocumentoId);
                ModelState.AddModelError(string.Empty, BusinessErrors.MessageOr(ex, "No fue posible actualizar el documento."));
                return View(model);
            }
        }

        // G2-17: baja de un documento (deja de contarse en la lista y en las alertas).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Flota", "FLOTA_MANTENIMIENTO")]
        public async Task<IActionResult> DeleteDocument(int documentoId, int? vehiculoId)
        {
            try
            {
                var (tipo, placa) = await _fleet.DeactivateDocumentAsync(documentoId);
                await RegistrarAuditoriaAsync("Dar de baja documento", "Flota", $"Documento #{documentoId} ({tipo}) del vehículo {placa} dado de baja.");
                TempData["SuccessMessage"] = "Documento dado de baja.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo dar de baja el documento {DocumentoId}.", documentoId);
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible dar de baja el documento.");
            }
            return RedirectToAction(nameof(Alerts), new { vehiculoId });
        }

        private async Task<IActionResult> MaintenanceViewWithFormAsync(MaintenanceFormViewModel nueva)
        {
            await PopulateVehiclesAsync();
            var vm = new MaintenanceIndexViewModel { Nueva = nueva, Ordenes = await _fleet.GetMaintenanceAsync(null, null) };
            return View(nameof(Maintenance), vm);
        }

        // Todos los vehículos: uno en reparación (inactivo) sigue necesitando órdenes y documentos.
        private async Task PopulateVehiclesAsync()
        {
            var vehiculos = await _logistics.GetVehiclesAsync(null);
            ViewBag.Vehiculos = vehiculos
                .Select(v => new VehicleOptionViewModel
                {
                    VehiculoId = v.VehiculoId,
                    Placa = v.Placa,
                    Descripcion = v.Descripcion + (v.EnMantenimiento ? " (en reparación)" : v.Activo ? string.Empty : " (inactivo)"),
                    Capacidad = v.Capacidad,
                    RutasAbiertas = v.RutasAbiertas
                })
                .ToList();
        }

        // Puede consultar y cerrar jornadas de otros: Administrador o el permiso FLOTA_KILOMETRAJE_ADMIN.
        private async Task<bool> CanAdministerMileageAsync()
        {
            var role = HttpContext.Session.GetString("UserRole") ?? string.Empty;
            return string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase)
                || await _permissionService.HasCodePermissionAsync(role, "FLOTA_KILOMETRAJE_ADMIN");
        }

        // Vehículos activos con su odómetro actual (para el kilometraje).
        private async Task PopulateMileageVehiclesAsync()
        {
            var vehiculos = await _logistics.GetVehiclesAsync(null);
            ViewBag.VehiculosKm = vehiculos.Where(v => v.Activo).ToList();
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
