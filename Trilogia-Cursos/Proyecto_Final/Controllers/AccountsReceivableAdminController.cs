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
        private readonly ILogger<AccountsReceivableAdminController> _logger;

        public AccountsReceivableAdminController(
            AdminDbService adminDbService,
            ILogger<AccountsReceivableAdminController> logger)
        {
            _adminDbService = adminDbService;
            _logger = logger;
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

                // POST-qa2-03 (CU-093-04): clientes con facturas vencidas (insignia «Vencida»).
                var vencidas = await _adminDbService.TryGetOverdueInvoicesAsync(null, _logger);
                ViewBag.FacturasVencidasPorCliente = vencidas
                    .GroupBy(v => v.UsuarioId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                return View(cuentas);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No se pudieron cargar las cuentas por cobrar.");
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

                // POST-qa2-03 (CU-093-04): facturas a crédito vencidas de este cliente.
                ViewBag.FacturasVencidas = await _adminDbService.TryGetOverdueInvoicesAsync(id, _logger);

                return View(cuenta);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No se pudo cargar la cuenta por cobrar {UserId}.", id);
                TempData["ErrorMessage"] =
                    "No fue posible cargar el detalle de la cuenta.";

                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Creditos", "CREDITOS_CONFIGURAR")]
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
                _logger.LogWarning(ex, "No se pudo actualizar la configuración de crédito del usuario {UserId}.", settingsForm.UsuarioId);
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }

            return RedirectToAction(
                nameof(Detail),
                new { id = settingsForm.UsuarioId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Creditos", "CREDITOS_MOVIMIENTOS")]
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
                _logger.LogWarning(ex, "No se pudo registrar el movimiento de crédito del usuario {UserId}.", movementForm.UsuarioId);

                // M-02: «abono mayor al saldo» (y las demás reglas del procedimiento) se explican en vez del texto genérico.
                decimal? deuda = null;
                if (EsRechazoPorSaldo(ex))
                {
                    try { deuda = (await _adminDbService.GetClientCreditDetailAsync(movementForm.UsuarioId))?.DeudaActual; }
                    catch (Exception) { /* sin el saldo se muestra el motivo del procedimiento */ }
                }

                TempData["ErrorMessage"] = MensajeMovimientoRechazado(ex, movementForm.TipoMovimiento, deuda);
            }

            return RedirectToAction(
                nameof(Detail),
                new { id = movementForm.UsuarioId });
        }

        /// <summary>sp_Admin_RegisterClientCreditMovement lanza 52014 cuando el abono o ajuste negativo supera la deuda.</summary>
        public const int ErrorMontoSuperaSaldo = 52014;

        private static Microsoft.Data.SqlClient.SqlException? ErrorDeNegocio(Exception ex)
        {
            for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
            {
                if (actual is Microsoft.Data.SqlClient.SqlException sql && sql.Number is >= 52000 and <= 52999)
                    return sql;
            }
            return null;
        }

        public static bool EsRechazoPorSaldo(Exception ex) => ErrorDeNegocio(ex)?.Number == ErrorMontoSuperaSaldo;

        /// <summary>
        /// M-02: el abono que supera el saldo dice cuánto se debe (₡X); las demás reglas del procedimiento
        /// (crédito bloqueado, sin límite…) se muestran tal cual; lo técnico conserva el texto genérico.
        /// </summary>
        public static string MensajeMovimientoRechazado(Exception ex, string? tipoMovimiento, decimal? deudaActual)
        {
            var sql = ErrorDeNegocio(ex);
            if (sql is null)
                return "No fue posible completar la operación solicitada.";

            if (sql.Number == ErrorMontoSuperaSaldo)
            {
                var que = string.Equals(tipoMovimiento, "Abono", StringComparison.OrdinalIgnoreCase) ? "El abono" : "El monto";
                return deudaActual.HasValue
                    ? $"{que} no puede superar el saldo pendiente del cliente (₡{deudaActual.Value:N2})."
                    : $"{que} no puede superar el saldo pendiente del cliente.";
            }

            return string.IsNullOrWhiteSpace(sql.Message)
                ? "No fue posible completar la operación solicitada."
                : sql.Message.Trim();
        }
    }
    }
