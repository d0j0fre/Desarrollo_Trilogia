using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-101 — Registrar y administrar proveedores.
    [AdminAuthorize("Compras", "PROVEEDORES_GESTIONAR")]
    public class SuppliersController : Controller
    {
        private readonly PurchasingDbService _purchasing;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<SuppliersController> _logger;

        public SuppliersController(PurchasingDbService purchasing, AdminDbService adminDbService, ILogger<SuppliersController> logger)
        {
            _purchasing = purchasing;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(bool? soloActivos, string? filtro)
        {
            var model = new SuppliersIndexViewModel
            {
                SoloActivos = soloActivos,
                Filtro = filtro,
                Proveedores = await _purchasing.GetSuppliersAsync(soloActivos, filtro),
                Nuevo = new SupplierFormViewModel { Activo = true }
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(SupplierFormViewModel nuevo)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revisá los datos del proveedor (el nombre es obligatorio).";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                var id = await _purchasing.UpsertSupplierAsync(nuevo);
                await RegistrarAuditoriaAsync(nuevo.ProveedorId > 0 ? "Editar proveedor" : "Crear proveedor", "Compras",
                    $"Proveedor #{id}: {nuevo.Nombre}.");
                TempData["SuccessMessage"] = "Proveedor guardado correctamente.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar proveedor.");
                TempData["ErrorMessage"] = "No fue posible guardar el proveedor.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int proveedorId)
        {
            try
            {
                await _purchasing.DeactivateSupplierAsync(proveedorId);
                await RegistrarAuditoriaAsync("Desactivar proveedor", "Compras", $"Proveedor #{proveedorId} marcado como inactivo.");
                TempData["SuccessMessage"] = "Proveedor desactivado.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar proveedor.");
                TempData["ErrorMessage"] = "No fue posible desactivar el proveedor.";
            }
            return RedirectToAction(nameof(Index));
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
