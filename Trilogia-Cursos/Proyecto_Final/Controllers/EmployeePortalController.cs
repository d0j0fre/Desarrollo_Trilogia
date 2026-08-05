using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [SessionAuthorize("Administrador", "Empleado", "Vendedor", "Chofer", "Bodeguero", "Cajero",
    "Facturador", "Compras", "Crédito y Cobro", "Soporte", "Auditor Interno", "Supervisor")]
    public class EmployeePortalController : Controller
    {
        private readonly EmployeesDbService _employeesDbService;
        private readonly AdminDbService _adminDbService;
        private readonly JornadasDbService _jornadasDbService;

        public EmployeePortalController(EmployeesDbService employeesDbService, AdminDbService adminDbService, JornadasDbService jornadasDbService)
        {
            _employeesDbService = employeesDbService;
            _adminDbService = adminDbService;
            _jornadasDbService = jornadasDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await _employeesDbService.GetEmployeePortalAsync(usuarioId);

            if (model.Perfil == null)
            {
                TempData["ErrorMessage"] = "No se encontró un perfil de empleado asociado a su usuario.";
                return RedirectToAction("Index", "Home");
            }

            model.MisJornadas = await _jornadasDbService.GetMisJornadasAsync(usuarioId, DateTime.Today.AddDays(-14), DateTime.Today);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTimeOff(EmployeeLeaveRequestFormViewModel model)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            if (model.FechaFin < model.FechaInicio)
            {
                ModelState.AddModelError(nameof(model.FechaFin), "La fecha final no puede ser menor a la fecha inicial.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Debe completar correctamente la solicitud de días libres.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var solicitudId = await _employeesDbService.CreateMyLeaveRequestAsync(usuarioId, model);

                await RegistrarAuditoriaAsync(
                    "Solicitud días libres",
                    "Empleados",
                    $"El usuario #{usuarioId} registró la solicitud de días libres #{solicitudId}.");

                TempData["SuccessMessage"] = "Solicitud enviada correctamente. Quedó pendiente de revisión administrativa.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible registrar la solicitud.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMyTaskStatus(int tareaId, string estado)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            if (tareaId <= 0 || string.IsNullOrWhiteSpace(estado))
            {
                TempData["ErrorMessage"] = "La solicitud no es válida.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _employeesDbService.UpdateMyTaskStatusAsync(usuarioId, tareaId, estado);

                await RegistrarAuditoriaAsync(
                    "Actualizar mi tarea",
                    "Empleados",
                    $"El usuario #{usuarioId} actualizó la tarea #{tareaId} al estado {estado}.");

                TempData["SuccessMessage"] = "Tarea actualizada correctamente.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible actualizar la tarea.";
            }

            return RedirectToAction(nameof(Index));
        }

        // CU-112 — el empleado registra o edita su jornada (borrador o envío a revisión)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarJornada(JornadaFormViewModel model)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revise las horas ingresadas para la jornada.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _jornadasDbService.GuardarMiJornadaAsync(usuarioId, model);

                await RegistrarAuditoriaAsync(
                    model.Enviar ? "Enviar jornada" : "Guardar jornada (borrador)",
                    "RRHH",
                    $"El usuario #{usuarioId} {(model.Enviar ? "envió" : "guardó")} su jornada del {model.Fecha:d}.");

                TempData["SuccessMessage"] = model.Enviar
                    ? "Jornada enviada a revisión del supervisor."
                    : "Jornada guardada como borrador.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible guardar la jornada.";
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
                accion,
                modulo,
                descripcion,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }
    }
}
