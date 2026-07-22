using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [SessionAuthorize("Chofer", "Administrador")]
    public sealed class DriverDeliveriesController : Controller
    {
        private readonly LogisticsDbService _logistics;
        private readonly AdminDbService _adminDbService;
        private readonly EmailService _emailService;
        private readonly IEvidenceStorageService _evidenceStorage;
        private readonly ILogger<DriverDeliveriesController> _logger;

        public DriverDeliveriesController(
            LogisticsDbService logistics,
            AdminDbService adminDbService,
            EmailService emailService,
            IEvidenceStorageService evidenceStorage,
            ILogger<DriverDeliveriesController> logger)
        {
            _logistics = logistics;
            _adminDbService = adminDbService;
            _emailService = emailService;
            _evidenceStorage = evidenceStorage;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = CurrentUserId();
            return View(await _logistics.GetDriverRoutesAsync(userId));
        }

        [HttpGet]
        public async Task<IActionResult> Route(int id)
        {
            if (id <= 0) return RedirectToAction(nameof(Index));
            try
            {
                var model = await _logistics.GetDriverRouteAsync(id, CurrentUserId());
                if (model is not null) return View(model);
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(exception, "Acceso rechazado a la ruta {RouteId} para el usuario {UserId}.", id, CurrentUserId());
            }

            TempData["ErrorMessage"] = "No fue posible consultar la ruta solicitada.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int rutaPedidoId,
            string nuevoEstado,
            string? syncGuid,
            string? motivoFallo)
        {
            var userId = CurrentUserId();
            var userName = CurrentUserName();
            if (!Guid.TryParse(syncGuid, out var guid)) guid = Guid.NewGuid();

            try
            {
                var result = await _logistics.UpdateDeliveryStatusAsync(
                    rutaPedidoId,
                    nuevoEstado,
                    guid,
                    motivoFallo,
                    userId,
                    userName);

                if (result is null)
                    return Json(new { ok = false, message = "No se pudo actualizar la entrega." });

                if (!result.Duplicado)
                {
                    await RegisterAuditAsync(
                        "Actualizar entrega",
                        "Entregas",
                        $"Pedido #{result.PedidoId}: estado de entrega {result.EstadoEntrega}.");

                    if (result.Notificar)
                    {
                        NotifyClientSafely(
                            result.ClienteCorreo,
                            result.ClienteNombre,
                            result.PedidoId,
                            result.EstadoEntrega == "Entregado" ? "Entregado" : "En ruta");
                    }
                }

                return Json(new
                {
                    ok = true,
                    estado = result.EstadoEntrega,
                    pedidoId = result.PedidoId,
                    rutaCompletada = result.RutaCompletada,
                    duplicado = result.Duplicado
                });
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(
                    exception,
                    "Cambio de estado rechazado para RutaPedido {RouteOrderId} por usuario {UserId}.",
                    rutaPedidoId,
                    userId);
                return Json(new { ok = false, message = "No fue posible aplicar el estado solicitado." });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error al actualizar el estado de RutaPedido {RouteOrderId}.", rutaPedidoId);
                return Json(new { ok = false, message = "No fue posible actualizar la entrega." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("evidence-upload")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> RegisterEvidence(
            int pedidoId,
            int rutaId,
            string tipo,
            string? observaciones,
            IFormFile? archivo,
            string? firmaBase64,
            CancellationToken cancellationToken)
        {
            var userId = CurrentUserId();
            var normalizedType = string.Equals(tipo, "Firma", StringComparison.OrdinalIgnoreCase) ? "Firma" : "Foto";
            StagedEvidence? staged = null;
            var evidenceId = 0;
            var committed = false;

            try
            {
                staged = normalizedType == "Firma"
                    ? await _evidenceStorage.StageSignatureAsync(firmaBase64, cancellationToken)
                    : await _evidenceStorage.StageUploadAsync(archivo, cancellationToken);

                if (staged is null)
                {
                    TempData["ErrorMessage"] = "Debe adjuntar una foto o firma válida.";
                    return RedirectToAction(nameof(Route), new { id = rutaId });
                }

                evidenceId = await _logistics.RegisterEvidenceAsync(
                    pedidoId,
                    rutaId,
                    normalizedType,
                    staged.StorageKey,
                    staged.ContentType,
                    observaciones,
                    userId,
                    CurrentUserName());

                await _evidenceStorage.CommitAsync(staged, cancellationToken);
                committed = true;
                await _logistics.MarkEvidenceReadyAsync(evidenceId, userId);

                await RegisterAuditAsync(
                    "Registrar evidencia",
                    "Entregas",
                    $"Evidencia ({normalizedType}) registrada para el pedido #{pedidoId}. ID: {evidenceId}.");
                TempData["SuccessMessage"] = "Evidencia registrada correctamente.";
            }
            catch (EvidenceValidationException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(
                    exception,
                    "Registro de evidencia rechazado para pedido {OrderId}, ruta {RouteId}, usuario {UserId}.",
                    pedidoId,
                    rutaId,
                    userId);
                TempData["ErrorMessage"] = "No tiene acceso a la entrega indicada o sus datos no son válidos.";
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al registrar evidencia para pedido {OrderId}, ruta {RouteId}.",
                    pedidoId,
                    rutaId);
                TempData["ErrorMessage"] = "No fue posible registrar la evidencia.";
            }
            finally
            {
                if (staged is not null && !committed)
                {
                    try { await _evidenceStorage.DeleteStageAsync(staged); }
                    catch (Exception exception) { _logger.LogWarning(exception, "No se pudo limpiar un archivo temporal de evidencia."); }
                }

                if (evidenceId > 0 && !committed)
                {
                    try { await _logistics.DeletePendingEvidenceAsync(evidenceId, userId); }
                    catch (Exception exception) { _logger.LogWarning(exception, "No se pudo compensar la evidencia pendiente {EvidenceId}.", evidenceId); }
                }
            }

            return RedirectToAction(nameof(Route), new { id = rutaId });
        }

        private int CurrentUserId() => HttpContext.Session.GetInt32("UserId") ?? 0;

        private string CurrentUserName() => HttpContext.Session.GetString("UserFullName") ?? "Usuario";

        private void NotifyClientSafely(string email, string name, int orderId, string status)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            try
            {
                var subject = $"Actualización de su pedido #{orderId} - {status}";
                var body =
                    $"<p>Hola {System.Net.WebUtility.HtmlEncode(name)},</p>" +
                    $"<p>Su pedido <strong>#{orderId}</strong> ahora está <strong>{System.Net.WebUtility.HtmlEncode(status)}</strong>.</p>" +
                    "<p>Gracias por su preferencia.<br/>Licorera La Bodega</p>";
                _emailService.SendEmail(email, subject, body);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No se pudo notificar al cliente del pedido {OrderId}.", orderId);
            }
        }

        private Task RegisterAuditAsync(string action, string module, string description) =>
            _adminDbService.CreateAuditLogAsync(
                HttpContext.Session.GetInt32("UserId"),
                HttpContext.Session.GetString("UserFullName"),
                HttpContext.Session.GetString("UserEmail"),
                HttpContext.Session.GetString("UserRole"),
                action,
                module,
                description,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
    }
}
