using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers.Admin
{
    [AdminAuthorize("Garantias", "GARANTIAS_GESTIONAR")]
    public sealed class WarrantyRequestsAdminController : Controller
    {
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "En revisión",
            "Aprobada",
            "Rechazada"
        };

        private readonly AdminDbService _adminDbService;
        private readonly ILogger<WarrantyRequestsAdminController> _logger;
        private readonly EmailService? _emailService;

        public WarrantyRequestsAdminController(
            AdminDbService adminDbService,
            ILogger<WarrantyRequestsAdminController> logger,
            EmailService? emailService = null)
        {
            _adminDbService = adminDbService;
            _logger = logger;
            _emailService = emailService;
        }

        // CU-144-05: el cliente recibe el resultado (Aprobada/Rechazada) y la resolución. Si el correo falla,
        // la evaluación NO se revierte: se registra y se avisa en pantalla.
        private async Task<string> NotificarResultadoAsync(WarrantyRequestViewModel? antes, int garantiaId, string estado, string resolucion)
        {
            const string ok = "La garantía se actualizó correctamente.";
            if (_emailService is null || antes is null || antes.UsuarioId <= 0
                || !(estado is "Aprobada" or "Rechazada")
                || string.Equals(antes.Estado, estado, StringComparison.OrdinalIgnoreCase))
            {
                return ok;
            }

            try
            {
                var cliente = await _adminDbService.GetClientByIdAsync(antes.UsuarioId);
                if (cliente is null || string.IsNullOrWhiteSpace(cliente.Correo))
                {
                    TempData["WarningMessage"] = "La garantía se actualizó, pero el cliente no tiene un correo registrado para notificarle.";
                    return string.Empty;
                }

                _emailService.SendEmail(
                    cliente.Correo,
                    estado == "Aprobada" ? $"Su garantía #{garantiaId} fue aprobada" : $"Su garantía #{garantiaId} no fue aprobada",
                    EmailTemplateBuilder.BuildWarrantyResultEmail(cliente.NombreCompleto, garantiaId, antes.Producto, estado, resolucion));
                return ok + " Se notificó el resultado al cliente por correo.";
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "La garantía {WarrantyId} fue evaluada, pero no se pudo notificar al cliente por correo.", garantiaId);
                TempData["WarningMessage"] = "La garantía se actualizó, pero no fue posible enviar el correo con el resultado al cliente. La evaluación se conservó.";
                return string.Empty;
            }
        }

        [HttpGet]
        public async Task<IActionResult> Index() =>
            View(await _adminDbService.GetWarrantyRequestsAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int garantiaId, string estado, string? resolucion)
        {
            estado = estado?.Trim() ?? string.Empty;
            if (garantiaId <= 0 || !AllowedStatuses.Contains(estado))
            {
                TempData["ErrorMessage"] = "La actualización de garantía no es válida.";
                return RedirectToAction(nameof(Index));
            }

            if ((estado == "Aprobada" || estado == "Rechazada") && string.IsNullOrWhiteSpace(resolucion))
            {
                TempData["ErrorMessage"] = "Debe indicar la resolución de la garantía.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                WarrantyRequestViewModel? antes = null;
                if (_emailService is not null && (estado is "Aprobada" or "Rechazada"))
                {
                    antes = (await _adminDbService.GetWarrantyRequestsAsync()).FirstOrDefault(g => g.GarantiaId == garantiaId);
                }

                await _adminDbService.UpdateWarrantyStatusAsync(
                    garantiaId,
                    estado,
                    resolucion,
                    userId,
                    HttpContext.Session.GetString("UserFullName") ?? "Administrador");
                var mensaje = await NotificarResultadoAsync(antes, garantiaId, estado, (resolucion ?? string.Empty).Trim());
                if (!string.IsNullOrEmpty(mensaje)) TempData["SuccessMessage"] = mensaje;
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(exception, "Actualización rechazada para la garantía {WarrantyId}.", garantiaId);
                TempData["ErrorMessage"] = "No fue posible aplicar el estado solicitado a la garantía.";
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error al actualizar la garantía {WarrantyId}.", garantiaId);
                TempData["ErrorMessage"] = "No fue posible actualizar la garantía.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
