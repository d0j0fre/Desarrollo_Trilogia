using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-191/192 — Registro y seguimiento de reclamos de clientes.
    [AdminAuthorize("Servicio al cliente", "RECLAMOS_GESTIONAR")]
    public class ReclamosController : Controller
    {
        private readonly ReclamosDbService _reclamos;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<ReclamosController> _logger;

        public ReclamosController(ReclamosDbService reclamos, AdminDbService adminDbService, ILogger<ReclamosController> logger)
        {
            _reclamos = reclamos;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? estado, string? buscar)
        {
            var model = new ReclamoFilterViewModel
            {
                Estado = estado,
                Buscar = buscar,
                Reclamos = await _reclamos.GetReclamosAsync(estado, buscar)
            };
            return View(model);
        }

        // CU-191 — Registrar reclamo
        [HttpGet]
        public async Task<IActionResult> Create(string? buscarCliente)
        {
            var model = new ReclamoFormViewModel
            {
                Clientes = await _reclamos.SearchClientsAsync(buscarCliente)
            };
            ViewBag.BuscarCliente = buscarCliente;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReclamoFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Clientes = await _reclamos.SearchClientsAsync(null);
                if (model.UsuarioId > 0) model.Facturas = await _reclamos.GetInvoicesByClientAsync(model.UsuarioId);
                return View(model);
            }

            var agenteId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var agenteNombre = HttpContext.Session.GetString("UserFullName") ?? "Agente";
            try
            {
                var id = await _reclamos.CreateReclamoAsync(model, agenteId, agenteNombre);
                await RegistrarAuditoriaAsync("Registrar reclamo", "Servicio al cliente",
                    $"Reclamo #{id} '{model.Asunto}' para el cliente #{model.UsuarioId}.");
                TempData["SuccessMessage"] = "Reclamo registrado correctamente.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                ModelState.AddModelError(string.Empty, "No fue posible completar la operación solicitada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar reclamo.");
                ModelState.AddModelError(string.Empty, "No fue posible registrar el reclamo.");
            }
            model.Clientes = await _reclamos.SearchClientsAsync(null);
            if (model.UsuarioId > 0) model.Facturas = await _reclamos.GetInvoicesByClientAsync(model.UsuarioId);
            return View(model);
        }

        // Facturas de un cliente (AJAX para el formulario de registro).
        [HttpGet]
        public async Task<IActionResult> InvoicesByClient(int usuarioId)
        {
            var facturas = await _reclamos.GetInvoicesByClientAsync(usuarioId);
            return Json(facturas.Select(f => new
            {
                f.FacturaId,
                texto = $"{f.NumeroFactura} · {f.FechaFactura:dd/MM/yyyy} · ₡{f.Total:N2}"
            }));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var reclamo = await _reclamos.GetReclamoByIdAsync(id);
            if (reclamo == null)
            {
                TempData["ErrorMessage"] = "No se encontró el reclamo solicitado.";
                return RedirectToAction(nameof(Index));
            }
            return View(reclamo);
        }

        // CU-192 — Cambiar estado / cerrar documentando la resolución
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(ReclamoUpdateStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revisá el estado y la resolución ingresada.";
                return RedirectToAction(nameof(Details), new { id = model.ReclamoId });
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Agente";
            try
            {
                await _reclamos.ChangeStatusAsync(model.ReclamoId, model.Estado, model.Resolucion, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Actualizar reclamo", "Servicio al cliente",
                    $"Reclamo #{model.ReclamoId} cambiado a estado {model.Estado}.");
                TempData["SuccessMessage"] = model.Estado == "Cerrado"
                    ? "Reclamo cerrado y resolución documentada."
                    : "Estado del reclamo actualizado.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar reclamo #{ReclamoId}.", model.ReclamoId);
                TempData["ErrorMessage"] = "Ocurrió un error al actualizar el reclamo.";
            }
            return RedirectToAction(nameof(Details), new { id = model.ReclamoId });
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
