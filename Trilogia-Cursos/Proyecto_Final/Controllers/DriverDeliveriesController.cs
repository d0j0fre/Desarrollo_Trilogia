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
        private readonly IDeliveryNotificationClient _notifications;
        private readonly IEvidenceStorageService _evidenceStorage;
        private readonly ILogger<DriverDeliveriesController> _logger;

        public DriverDeliveriesController(
            LogisticsDbService logistics,
            AdminDbService adminDbService,
            IEvidenceStorageService evidenceStorage,
            ILogger<DriverDeliveriesController> logger,
            IDeliveryNotificationClient notifications)
        {
            _logistics = logistics;
            _adminDbService = adminDbService;
            _notifications = notifications;
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
            if (!Guid.TryParse(syncGuid, out var guid) || guid == Guid.Empty)
                return BadRequest(new { ok = false, message = "El identificador de sincronización no es válido. Volvé a abrir la entrega." });
            if (!Guid.TryParse(HttpContext.Session.GetString("SecurityStamp"), out _))
                return Unauthorized(new { ok = false, message = "La sesión no está vigente. Iniciá sesión nuevamente." });

            try
            {
                var result = await _logistics.UpdateDeliveryStatusAsync(
                    rutaPedidoId,
                    nuevoEstado,
                    guid,
                    motivoFallo,
                    userId,
                    userName,
                    OriginalLoginSession.ValidatedRequest(HttpContext), HttpContext.RequestAborted);

                if (result is null)
                    return Json(new { ok = false, message = "No se pudo actualizar la entrega." });

                // A duplicate business receipt can be the first response the caller receives.
                // Recover/request the event notification too; SQL prevents a second SMTP claim.
                string? notification = null;
                if (nuevoEstado is "EnRuta" or "Entregado")
                    notification = (await NotifyDeliverySafely(rutaPedidoId, guid))?.State ?? "Unconfirmed";

                if (notification is not null)
                {
                    var key = notification == "SmtpAccepted" ? "SuccessMessage" : "ErrorMessage";
                    TempData[key] = NotificationMessage(notification);
                }

                return Json(new
                {
                    ok = true,
                    estado = result.EstadoEntrega,
                    pedidoId = result.PedidoId,
                    rutaCompletada = result.RutaCompletada,
                    duplicado = result.Duplicado,
                    notificationState = notification,
                    notificationMessage = notification is null ? null : NotificationMessage(notification)
                });
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(
                    exception,
                    "Cambio de estado rechazado para RutaPedido {RouteOrderId} por usuario {UserId}.",
                    rutaPedidoId,
                    userId);
                var status = exception.Number switch
                {
                    56801 or 56802 or 56328 => StatusCodes.Status401Unauthorized,
                    52074 => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status409Conflict
                };
                return StatusCode(status, new { ok = false, message = "No fue posible aplicar el estado solicitado." });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error al actualizar el estado de RutaPedido {RouteOrderId}.", rutaPedidoId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, message = "No fue posible actualizar la entrega." });
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
            var moved = false;

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
                moved = true;
                await _logistics.MarkEvidenceReadyAsync(evidenceId, userId, staged.Sha256 ?? throw new EvidenceValidationException("Integridad del archivo no acreditada."), staged.Length);
                committed = true;
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
                    try
                    {
                        if (moved) await _evidenceStorage.DeleteUnpublishedAsync(staged, cancellationToken);
                        else await _evidenceStorage.DeleteStageAsync(staged);
                    }
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

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("delivery-notification")]
        public async Task<IActionResult> NotifyEmail(int rutaPedidoId)
        {
            if (rutaPedidoId <= 0) return BadRequest();
            try
            {
                var receipt = await _notifications.NotifyAsync(rutaPedidoId, Guid.Empty, HttpContext.RequestAborted);
                return Json(new { ok = true, state = receipt.State, message = NotificationMessage(receipt.State) });
            }
            catch (UnauthorizedAccessException error)
            {
                LogNotification(error);
                return Unauthorized(new { ok = false, message = "La sesión no está vigente. Iniciá sesión nuevamente." });
            }
            catch (Exception error) when (error is DeliveryNotificationTransportException or HttpRequestException or OperationCanceledException)
            {
                LogNotification(error);
                return StatusCode(error is DeliveryNotificationTransportException { Status: 403 or 404 } transport ? transport.Status : 503,
                    new { ok = false, message = NotificationMessage("Unconfirmed") });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("delivery-notification")]
        public async Task<IActionResult> EmailReceipt(int rutaPedidoId)
        {
            if (rutaPedidoId <= 0) return BadRequest();
            try
            {
                var receipt = await _notifications.ReceiptAsync(rutaPedidoId, Guid.Empty, Guid.Empty, HttpContext.RequestAborted);
                return Json(new { ok = true, state = receipt.State, message = NotificationMessage(receipt.State) });
            }
            catch (UnauthorizedAccessException error)
            {
                LogNotification(error);
                return Unauthorized(new { ok = false, message = "La sesión no está vigente. Iniciá sesión nuevamente." });
            }
            catch (Exception error) when (error is DeliveryNotificationTransportException or HttpRequestException or OperationCanceledException)
            {
                LogNotification(error);
                return StatusCode(error is DeliveryNotificationTransportException { Status: 403 or 404 } transport ? transport.Status : 503,
                    new { ok = false, message = NotificationMessage("Unconfirmed") });
            }
        }

        private async Task<JJ.Commerce.DeliveryNotificationReceipt?> NotifyDeliverySafely(int routeOrder, Guid sync)
        {
            try { return await _notifications.NotifyAsync(routeOrder, sync, HttpContext.RequestAborted); }
            catch (Exception error) { LogNotification(error); return null; }
        }
        private void LogNotification(Exception error) => _logger.LogWarning("Delivery notification unconfirmed. Type {Type}; status {Status}.",
            error.GetType().Name, (error as DeliveryNotificationTransportException)?.Status);
        private static string NotificationMessage(string state) => state switch
        {
            "SmtpAccepted" => "El servidor de correo aceptó el aviso. Esto no acredita su entrega al buzón.",
            "Pending" => "El aviso quedó registrado y todavía no se inició el envío. Puede continuar el mismo intento.",
            "Sending" => "Hay un aviso en curso. Consulte su estado antes de realizar otra acción.",
            "Unknown" => "El resultado del aviso es desconocido. No se reenviará automáticamente para evitar duplicados.",
            "AlreadyRequested" => "Ya existe una solicitud para este evento. No se generó otro envío ni se recuperó un intento de otra sesión.",
            "None" => "Esta sesión no tiene un intento de correo para el evento. Puede solicitar el primer aviso; un envío previo de otra sesión no se repetirá.",
            "NoEvent" => "Todavía no hay una actualización de entrega confirmada para notificar.",
            _ => "La entrega y el correo son operaciones separadas. No se pudo confirmar el estado del aviso; consulte su recibo antes de intentarlo otra vez."
        };
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
