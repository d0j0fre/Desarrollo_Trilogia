using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-222 — Registro de gastos operativos y cuentas presupuestarias.
    [AdminAuthorize("Compras", "GASTOS_REGISTRAR")]
    public class ExpensesController : Controller
    {
        private readonly ExpensesDbService _expenses;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<ExpensesController> _logger;

        public ExpensesController(ExpensesDbService expenses, AdminDbService adminDbService, ILogger<ExpensesController> logger)
        {
            _expenses = expenses;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        // CU-222 — Panel de gastos y presupuesto
        [HttpGet]
        public async Task<IActionResult> Index(int? anio, int? mes, int? cuentaId)
        {
            var (a, m) = NormalizarPeriodo(anio, mes);
            var model = new ExpensesIndexViewModel
            {
                Anio = a,
                Mes = m,
                CuentaId = cuentaId,
                Gastos = await _expenses.GetExpensesAsync(a, m, cuentaId),
                Resumen = await _expenses.GetBudgetSummaryAsync(a, m),
                Cuentas = await _expenses.GetAccountOptionsAsync(),
                Nuevo = new GastoFormViewModel { Fecha = DateTime.Today }
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(GastoFormViewModel nuevo, int anio, int mes, int? cuentaFiltro)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revisá los datos del gasto (cuenta, monto y concepto).";
                return RedirectToAction(nameof(Index), new { anio, mes, cuentaId = cuentaFiltro });
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Compras";
            try
            {
                var id = await _expenses.RegisterExpenseAsync(nuevo, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Registrar gasto operativo", "Compras",
                    $"Gasto #{id} de ₡{nuevo.Monto:N2} en cuenta #{nuevo.CuentaId}: {nuevo.Concepto}.");
                TempData["SuccessMessage"] = "Gasto registrado correctamente.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar gasto.");
                TempData["ErrorMessage"] = "No fue posible registrar el gasto.";
            }
            return RedirectToAction(nameof(Index), new { anio, mes, cuentaId = cuentaFiltro });
        }

        // CU-222 — Gestión de cuentas presupuestarias
        [HttpGet]
        public async Task<IActionResult> Accounts()
        {
            var model = new AccountsViewModel
            {
                Cuentas = await _expenses.GetAccountsAsync(),
                Nueva = new CuentaViewModel { Activo = true }
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAccount(CuentaViewModel nueva)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revisá los datos de la cuenta presupuestaria.";
                return RedirectToAction(nameof(Accounts));
            }
            try
            {
                await _expenses.UpsertAccountAsync(nueva);
                await RegistrarAuditoriaAsync(nueva.CuentaId > 0 ? "Editar cuenta presupuestaria" : "Crear cuenta presupuestaria", "Compras",
                    $"Cuenta '{nueva.Codigo} - {nueva.Nombre}' con presupuesto ₡{nueva.PresupuestoMensual:N2}.");
                TempData["SuccessMessage"] = "Cuenta presupuestaria guardada.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar cuenta presupuestaria.");
                TempData["ErrorMessage"] = "No fue posible guardar la cuenta.";
            }
            return RedirectToAction(nameof(Accounts));
        }

        private static (int anio, int mes) NormalizarPeriodo(int? anio, int? mes)
        {
            var hoy = DateTime.Now;
            var a = anio is >= 2000 and <= 2100 ? anio.Value : hoy.Year;
            var m = mes is >= 1 and <= 12 ? mes.Value : hoy.Month;
            return (a, m);
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
