using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Creditos")]
    public class AccountsReceivableAdminController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public AccountsReceivableAdminController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? buscar,
            string? estadoCredito)
        {
            ViewBag.Buscar = buscar;
            ViewBag.EstadoCredito = estadoCredito;

            try
            {
                var cuentas = await _adminDbService
                    .GetClientCreditsAsync(buscar, estadoCredito);

                return View(cuentas);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] =
                    "No fue posible cargar las cuentas por cobrar.";

                return View(new List<ClientCreditListItemViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] =
                    "La cuenta por cobrar solicitada no es válida.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                var cuenta = await _adminDbService
                    .GetClientCreditDetailAsync(id);

                if (cuenta == null)
                {
                    TempData["ErrorMessage"] =
                        "No se encontró la cuenta por cobrar solicitada.";

                    return RedirectToAction(nameof(Index));
                }

                return View(cuenta);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] =
                    "No fue posible cargar el detalle de la cuenta.";

                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(
            [Bind(Prefix = "SettingsForm")]
    ClientCreditSettingsViewModel settingsForm)
        {
            if (settingsForm.UsuarioId <= 0)
            {
                TempData["ErrorMessage"] = "El cliente seleccionado no es válido.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revise la configuración del crédito.";

                return RedirectToAction(
                    nameof(Detail),
                    new { id = settingsForm.UsuarioId });
            }

            try
            {
                await _adminDbService.UpdateClientCreditSettingsAsync(
                    settingsForm,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName") ?? "Administrador");

                TempData["SuccessMessage"] =
                    "La configuración del crédito se actualizó correctamente.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(
                nameof(Detail),
                new { id = settingsForm.UsuarioId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterMovement(
            [Bind(Prefix = "MovementForm")]
    ClientCreditMovementFormViewModel movementForm)
        {
            if (movementForm.UsuarioId <= 0)
            {
                TempData["ErrorMessage"] = "El cliente seleccionado no es válido.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revise los datos del movimiento.";

                return RedirectToAction(
                    nameof(Detail),
                    new { id = movementForm.UsuarioId });
            }

            try
            {
                await _adminDbService.RegisterClientCreditMovementAsync(
                    movementForm,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName") ?? "Administrador");

                TempData["SuccessMessage"] =
                    "El movimiento se registró correctamente.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(
                nameof(Detail),
                new { id = movementForm.UsuarioId });
        }
    }
    }