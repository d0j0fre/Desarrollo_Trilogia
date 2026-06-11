using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize]
    public class CreditsController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public CreditsController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, string? estadoCredito)
        {
            var clientes = await _adminDbService.GetClientCreditsAsync(buscar, estadoCredito);

            var model = new ClientCreditFilterViewModel
            {
                Buscar = buscar,
                EstadoCredito = estadoCredito,
                Clientes = clientes
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _adminDbService.GetClientCreditDetailAsync(id);

            if (model == null)
            {
                TempData["ErrorMessage"] = "No se encontró el cliente solicitado.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(ClientCreditSettingsViewModel model)
        {
            if (model.UsuarioId <= 0)
            {
                TempData["ErrorMessage"] = "La solicitud no es válida.";
                return RedirectToAction(nameof(Index));
            }

            if ((!model.CreditoActivo || model.CreditoBloqueado) && string.IsNullOrWhiteSpace(model.MotivoBloqueo))
            {
                ModelState.AddModelError(nameof(model.MotivoBloqueo), "Debe indicar un motivo si desactiva o bloquea el crédito.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revise los datos de configuración del crédito.";
                return RedirectToAction(nameof(Details), new { id = model.UsuarioId });
            }

            try
            {
                await _adminDbService.UpdateClientCreditSettingsAsync(
                    model,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName"));

                await RegistrarAuditoriaAsync(
                    "Configurar",
                    "Créditos",
                    $"Se actualizó la configuración de crédito del cliente #{model.UsuarioId}. Límite: ₡{model.LimiteCredito:N2}, activo: {model.CreditoActivo}, bloqueado: {model.CreditoBloqueado}.");

                TempData["SuccessMessage"] = "Configuración de crédito actualizada correctamente.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Ocurrió un error al actualizar la configuración del crédito.";
            }

            return RedirectToAction(nameof(Details), new { id = model.UsuarioId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterMovement(ClientCreditMovementFormViewModel model)
        {
            if (model.UsuarioId <= 0)
            {
                TempData["ErrorMessage"] = "La solicitud no es válida.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revise los datos del movimiento de crédito.";
                return RedirectToAction(nameof(Details), new { id = model.UsuarioId });
            }

            try
            {
                var movimientoId = await _adminDbService.RegisterClientCreditMovementAsync(
                    model,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName"));

                await RegistrarAuditoriaAsync(
                    "Registrar movimiento",
                    "Créditos",
                    $"Se registró el movimiento #{movimientoId} de tipo {model.TipoMovimiento} por ₡{model.Monto:N2} para el cliente #{model.UsuarioId}.");

                TempData["SuccessMessage"] = "Movimiento de crédito registrado correctamente.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Ocurrió un error al registrar el movimiento de crédito.";
            }

            return RedirectToAction(nameof(Details), new { id = model.UsuarioId });
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
