using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Avisos de quien usa la aplicación: ruta asignada o reasignada al chofer
    /// (BUG-08-01, BUG-10-08). Cualquier perfil con acceso a la app tiene su
    /// bandeja; el usuario sale del token.
    /// </summary>
    [Route("api/mobile/v1/me/notifications")]
    [Authorize]
    [RequirePermission(MobilePermissions.Access)]
    public sealed class MobileNotificationsController : MobileControllerBase
    {
        private readonly INotificationsMobileDbService _notifications;
        private readonly ILogger<MobileNotificationsController> _logger;

        public MobileNotificationsController(
            INotificationsMobileDbService notifications,
            ILogger<MobileNotificationsController> logger)
        {
            _notifications = notifications;
            _logger = logger;
        }

        [HttpGet]
        [EnableRateLimiting("mobile-read")]
        public Task<IActionResult> List(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar notificaciones", async () =>
                Ok(await _notifications.GetAsync(CurrentUserId, cancellationToken)));

        [HttpPost("{id:int}/read")]
        [EnableRateLimiting("mobile-write")]
        public Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
        {
            if (id <= 0) return Task.FromResult(NotFoundOrForbidden());

            return GuardAsync(_logger, "marcar notificación", async () =>
                Ok(new { marcadas = await _notifications.MarkReadAsync(CurrentUserId, id, cancellationToken) }));
        }

        [HttpPost("read-all")]
        [EnableRateLimiting("mobile-write")]
        public Task<IActionResult> MarkAllRead(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "marcar notificaciones", async () =>
                Ok(new { marcadas = await _notifications.MarkReadAsync(CurrentUserId, null, cancellationToken) }));
    }
}
