using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // BUG-08-01 / BUG-10-08 — Campana y bandeja de avisos del usuario.
    // Cada quien ve y marca solo los suyos: el usuario sale de la sesión.
    [SessionAuthorize]
    public sealed class NotificationsController : Controller
    {
        private readonly NotificationsDbService _notifications;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(NotificationsDbService notifications, ILogger<NotificationsController> logger)
        {
            _notifications = notifications;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                return View(await _notifications.GetAsync(CurrentUserId()));
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudieron leer las notificaciones del usuario {UserId}.", CurrentUserId());
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        // Abre lo que el aviso menciona y lo deja leído.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(int id)
        {
            if (id <= 0) return NotFound();

            Models.Admin.UserNotificationViewModel? notification;
            try
            {
                // El destino sale del aviso guardado, no del formulario.
                notification = (await _notifications.GetAsync(CurrentUserId()))
                    .FirstOrDefault(n => n.NotificacionId == id);
                if (notification is null) return NotFound();
                await _notifications.MarkReadAsync(CurrentUserId(), id);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudo abrir la notificación {Id}.", id);
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            if (notification.EntidadTipo == "Ruta" && notification.EntidadId > 0)
            {
                var role = HttpContext.Session.GetString("UserRole");
                return string.Equals(role, "Chofer", StringComparison.OrdinalIgnoreCase)
                    ? RedirectToAction("Route", "DriverDeliveries", new { id = notification.EntidadId })
                    : RedirectToAction("Detail", "RoutesAdmin", new { id = notification.EntidadId });
            }

            // BUG-23-05: un mensaje directo se lee y se responde en el chat.
            if (notification.EntidadTipo == "Chat") return RedirectToAction("Index", "Chat");

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            try
            {
                await _notifications.MarkReadAsync(CurrentUserId(), null);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudieron marcar las notificaciones del usuario {UserId}.", CurrentUserId());
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            return RedirectToAction(nameof(Index));
        }

        private int CurrentUserId() => HttpContext.Session.GetInt32("UserId") ?? 0;
    }
}
