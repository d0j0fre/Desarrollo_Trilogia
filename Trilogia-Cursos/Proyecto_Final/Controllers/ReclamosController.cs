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
        private readonly EmailService? _emailService;

        public ReclamosController(ReclamosDbService reclamos, AdminDbService adminDbService, ILogger<ReclamosController> logger, EmailService? emailService = null)
        {
            _reclamos = reclamos;
            _adminDbService = adminDbService;
            _logger = logger;
            _emailService = emailService;
        }

        // POST-qa3-02: al cerrar el reclamo el cliente recibe también un correo con la resolución (además del aviso
        // en su bandeja, 0327). Si el correo falla, el cierre NO se revierte: se registra y se avisa en pantalla.
        private void NotificarCierre(ReclamoDetailViewModel? antes, ReclamoUpdateStatusViewModel model)
        {
            if (_emailService is null || antes is null || model.Estado != "Cerrado" || antes.Estado == "Cerrado") return;

            if (string.IsNullOrWhiteSpace(antes.ClienteCorreo))
            {
                TempData["WarningMessage"] = "El reclamo se cerró, pero el cliente no tiene un correo registrado para notificarle.";
                return;
            }

            try
            {
                _emailService.SendEmail(
                    antes.ClienteCorreo,
                    $"Su reclamo #{model.ReclamoId} fue resuelto",
                    EmailTemplateBuilder.BuildClaimClosedEmail(antes.ClienteNombre, model.ReclamoId, antes.Asunto, model.Resolucion ?? string.Empty));
                TempData["SuccessMessage"] = "Reclamo cerrado y resolución documentada. Se notificó al cliente por correo.";
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "El reclamo {ReclamoId} se cerró, pero no se pudo notificar al cliente por correo.", model.ReclamoId);
                TempData.Remove("SuccessMessage");
                TempData["WarningMessage"] = "El reclamo se cerró y la resolución quedó documentada, pero no fue posible enviar el correo al cliente.";
            }
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
                ModelState.AddModelError(string.Empty, BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada."));
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
                TempData["ErrorMessage"] = "Revise el estado y la resolución ingresada.";
                return RedirectToAction(nameof(Details), new { id = model.ReclamoId });
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Agente";
            try
            {
                var antes = _emailService is not null && model.Estado == "Cerrado" ? await _reclamos.GetReclamoByIdAsync(model.ReclamoId) : null;
                await _reclamos.ChangeStatusAsync(model.ReclamoId, model.Estado, model.Resolucion, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Actualizar reclamo", "Servicio al cliente",
                    $"Reclamo #{model.ReclamoId} cambiado a estado {model.Estado}.");
                TempData["SuccessMessage"] = model.Estado == "Cerrado"
                    ? "Reclamo cerrado y resolución documentada."
                    : "Estado del reclamo actualizado.";
                NotificarCierre(antes, model);
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
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
