using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Clientes")]
    public class ClientsController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public ClientsController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, string? estado)
        {
            var clientes = await _adminDbService.GetClientsAsync(buscar, estado);

            var model = new ClientFilterViewModel
            {
                Buscar = buscar,
                Estado = estado,
                Clientes = clientes
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ClientFormViewModel { Activo = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClientFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Contrasena))
            {
                ModelState.AddModelError(nameof(model.Contrasena), "La contraseña es obligatoria para registrar un cliente.");
            }

            if (!model.Activo && string.IsNullOrWhiteSpace(model.MotivoInactivacion))
            {
                ModelState.AddModelError(nameof(model.MotivoInactivacion), "Debe indicar un motivo si registra el cliente como inactivo.");
            }

            if (!ModelState.IsValid) return View(model);

            try
            {
                var usuarioId = await _adminDbService.CreateClientAsync(model);

                await RegistrarAuditoriaAsync(
                    "Crear",
                    "Clientes",
                    $"Se registró el cliente {model.NombreCompleto} con identificador #{usuarioId}.");

                TempData["SuccessMessage"] = "Cliente registrado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo registrar el cliente. Revise los datos e intente nuevamente.");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error al registrar el cliente. Intente nuevamente.");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _adminDbService.GetClientByIdAsync(id);

            if (model == null)
            {
                TempData["ErrorMessage"] = "No se encontró el cliente solicitado.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ClientFormViewModel model)
        {
            if (!model.Activo && string.IsNullOrWhiteSpace(model.MotivoInactivacion))
            {
                ModelState.AddModelError(nameof(model.MotivoInactivacion), "Debe indicar un motivo para inactivar el cliente.");
            }

            if (!ModelState.IsValid) return View(model);

            try
            {
                await _adminDbService.UpdateClientAsync(model);

                await RegistrarAuditoriaAsync(
                    "Editar",
                    "Clientes",
                    $"Se actualizó la información del cliente {model.NombreCompleto}.");

                TempData["SuccessMessage"] = "Cliente actualizado correctamente.";
                return RedirectToAction(nameof(Details), new { id = model.UsuarioId });
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo actualizar el cliente. Revise los datos e intente nuevamente.");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error al actualizar el cliente. Intente nuevamente.");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var cliente = await _adminDbService.GetClientDetailAsync(id);

            if (cliente == null)
            {
                TempData["ErrorMessage"] = "No se encontró el cliente solicitado.";
                return RedirectToAction(nameof(Index));
            }

            return View(cliente);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int usuarioId, string? motivo, string? buscar, string? estado, string? returnTo)
        {
            if (usuarioId <= 0)
            {
                TempData["ErrorMessage"] = "La solicitud no es válida.";
                return RedirectToAction(nameof(Index), new { buscar, estado });
            }

            try
            {
                var cliente = await _adminDbService.GetClientByIdAsync(usuarioId);
                var nuevoEstado = await _adminDbService.ToggleClientStatusAsync(usuarioId, motivo);

                await RegistrarAuditoriaAsync(
                    nuevoEstado ? "Activar" : "Inactivar",
                    "Clientes",
                    nuevoEstado
                        ? $"Se reactivó el cliente #{usuarioId} {(cliente == null ? string.Empty : cliente.NombreCompleto)}."
                        : $"Se inactivó el cliente #{usuarioId} {(cliente == null ? string.Empty : cliente.NombreCompleto)}. Motivo: {(string.IsNullOrWhiteSpace(motivo) ? "No indicado" : motivo.Trim())}");

                TempData["SuccessMessage"] = nuevoEstado
                    ? "Cliente reactivado correctamente."
                    : "Cliente inactivado correctamente.";
            }
            catch (InvalidOperationException)
            {
                TempData["ErrorMessage"] = "No se pudo cambiar el estado del cliente.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Ocurrió un error al cambiar el estado del cliente.";
            }

            if (string.Equals(returnTo, "details", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Details), new { id = usuarioId });
            }

            return RedirectToAction(nameof(Index), new { buscar, estado });
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
