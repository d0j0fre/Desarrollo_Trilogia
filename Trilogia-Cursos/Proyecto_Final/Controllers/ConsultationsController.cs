using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Consultas")]
    public class ConsultationsController : Controller
    {
        private readonly AdminDbService _adminDbService;
        private readonly EmailService _emailService;
        private readonly ILogger<ConsultationsController> _logger;

        public ConsultationsController(AdminDbService adminDbService, EmailService emailService, ILogger<ConsultationsController> logger)
        {
            _adminDbService = adminDbService;
            _emailService = emailService;
            _logger = logger;
        }

        // Estados en los que la consulta se considera resuelta y se avisa al cliente (CU-032).
        public static bool IsResolution(string? estadoAnterior, string? estadoNuevo)
        {
            var nuevo = estadoNuevo?.Trim();
            var esResuelta = string.Equals(nuevo, "Atendida", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nuevo, "Cerrada", StringComparison.OrdinalIgnoreCase);

            return esResuelta && !string.Equals(estadoAnterior?.Trim(), nuevo, StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? estado, string? buscar)
        {
            var consultas = await _adminDbService.GetConsultationsAsync(estado, buscar);

            var model = new ConsultationFilterViewModel
            {
                Estado = estado,
                Buscar = buscar,
                Consultas = consultas
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var consulta = await _adminDbService.GetConsultationByIdAsync(id);

            if (consulta == null)
            {
                TempData["ErrorMessage"] = "No se encontró la consulta solicitada.";
                return RedirectToAction(nameof(Index));
            }

            return View(consulta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(ConsultationUpdateStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revisá el estado y la observación ingresada.";
                return RedirectToAction(nameof(Details), new { id = model.ConsultaId });
            }

            try
            {
                var consultaAnterior = await _adminDbService.GetConsultationByIdAsync(model.ConsultaId);

                await _adminDbService.UpdateConsultationStatusAsync(
                    model.ConsultaId,
                    model.Estado,
                    model.RespuestaInterna,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName"));

                await RegistrarAuditoriaAsync(
                    "Actualizar consulta",
                    "Consultas",
                    $"Se actualizó la consulta #{model.ConsultaId} al estado {model.Estado}.");

                TempData["SuccessMessage"] = "Consulta actualizada correctamente.";

                if (consultaAnterior != null
                    && !string.IsNullOrWhiteSpace(consultaAnterior.Correo)
                    && IsResolution(consultaAnterior.Estado, model.Estado))
                {
                    try
                    {
                        _emailService.SendEmail(
                            consultaAnterior.Correo,
                            $"Tu consulta fue resuelta - {consultaAnterior.Asunto}",
                            EmailTemplateBuilder.BuildConsultationResolvedEmail(consultaAnterior.Nombre, consultaAnterior.Asunto, model.Estado));

                        TempData["SuccessMessage"] = "Consulta actualizada correctamente. Se notificó la resolución al cliente por correo.";
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "No se pudo notificar la resolución de la consulta {ConsultaId}. Solicitud {TraceId}.", model.ConsultaId, HttpContext.TraceIdentifier);
                        TempData["ErrorMessage"] = "La consulta se actualizó, pero no fue posible notificar la resolución al cliente por correo.";
                    }
                }
            }
            catch (InvalidOperationException)
            {
                TempData["ErrorMessage"] = "No se pudo actualizar la consulta.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Ocurrió un error al actualizar la consulta.";
            }

            return RedirectToAction(nameof(Details), new { id = model.ConsultaId });
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
