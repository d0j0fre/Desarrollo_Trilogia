using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Gestión rápida para administración y gerencia: métricas del negocio y
    /// control de rutas (despachar, reasignar chofer o vehículo).
    ///
    /// No reemplaza al sitio web: crear rutas, secuenciarlas o liquidarlas
    /// sigue siendo trabajo de escritorio.
    /// </summary>
    [Route("api/mobile/v1/management")]
    [Authorize]
    [RequirePermission(MobilePermissions.Access)]
    public sealed class MobileManagementController : MobileControllerBase
    {
        private readonly IManagementMobileDbService _management;
        private readonly IMobileAuditService _audit;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _time;
        private readonly ILogger<MobileManagementController> _logger;

        public MobileManagementController(
            IManagementMobileDbService management,
            IMobileAuditService audit,
            IServiceScopeFactory scopeFactory,
            TimeProvider time,
            ILogger<MobileManagementController> logger)
        {
            _management = management;
            _audit = audit;
            _scopeFactory = scopeFactory;
            _time = time;
            _logger = logger;
        }

        /// <summary>Rangos fijos a propósito: en el teléfono no se arma un reporte, se mira cómo va.</summary>
        [HttpGet("dashboard")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.Dashboard)]
        public Task<IActionResult> Dashboard([FromQuery] string? rango, CancellationToken cancellationToken)
        {
            var today = CostaRicaToday();
            DateTime from;
            switch ((rango ?? "hoy").Trim().ToLowerInvariant())
            {
                case "hoy": from = today; break;
                case "semana": from = today.AddDays(-6); break;
                case "mes": from = new DateTime(today.Year, today.Month, 1); break;
                case "30dias": from = today.AddDays(-29); break;
                default: return Task.FromResult(InvalidRequest("El rango indicado no es válido."));
            }

            return GuardAsync(_logger, "consultar métricas", async () =>
                Ok(await _management.GetDashboardAsync(from, today, cancellationToken)));
        }

        [HttpGet("routes")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.ManageRoutes)]
        public Task<IActionResult> Routes([FromQuery] string? estado, [FromQuery] string? buscar, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar rutas", async () =>
                Ok(await _management.GetRoutesAsync(estado, buscar, cancellationToken)));

        [HttpGet("routes/{routeId:int}")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.ManageRoutes)]
        public Task<IActionResult> Route(int routeId, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar ruta", async () =>
            {
                var route = await _management.GetRouteAsync(routeId, cancellationToken);
                return route is null ? NotFoundOrForbidden() : Ok(route);
            });

        [HttpGet("drivers")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.ManageRoutes)]
        public Task<IActionResult> Drivers(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar choferes", async () => Ok(await _management.GetDriversAsync(cancellationToken)));

        [HttpGet("vehicles")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.ManageRoutes)]
        public Task<IActionResult> Vehicles(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar vehículos", async () => Ok(await _management.GetVehiclesAsync(cancellationToken)));

        [HttpPost("routes/{routeId:int}/reassign")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.ManageRoutes)]
        public async Task<IActionResult> Reassign(int routeId, [FromBody] ReassignRouteRequest request, CancellationToken cancellationToken)
        {
            if (routeId <= 0) return NotFoundOrForbidden();
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (request.NuevoChoferId <= 0) return InvalidRequest("Elegí el chofer.");
            var reason = request.Motivo?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
                return InvalidRequest("Explicá el motivo de la reasignación (al menos 5 caracteres).");
            if (!TryReadSyncGuid(request.SyncGuid, out var syncGuid))
                return InvalidRequest("Falta un identificador de sincronización válido.");

            return await GuardAsync(_logger, "reasignar ruta", async () =>
            {
                var result = await _management.ReassignRouteAsync(
                    routeId, request.NuevoChoferId, request.NuevoVehiculoId, reason, syncGuid,
                    CurrentUserId, CurrentUserName, cancellationToken);
                if (result is null) return Unavailable();

                if (!result.Duplicado)
                {
                    await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                        "Reasignar ruta", "Rutas",
                        $"Ruta {result.Codigo}: de {result.ChoferAnterior} a {result.Chofer}, vehículo {result.VehiculoPlaca}. Motivo: {reason}.",
                        ClientIp, ClientUserAgent, cancellationToken);
                }

                return Ok(result);
            });
        }

        /// <summary>
        /// Despacha una ruta planificada. Igual que el sitio web, avisa por correo
        /// a cada cliente que su pedido va en camino; el aviso es de mejor
        /// esfuerzo y no demora la respuesta.
        /// </summary>
        [HttpPost("routes/{routeId:int}/dispatch")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.ManageRoutes)]
        public Task<IActionResult> Dispatch(int routeId, CancellationToken cancellationToken)
        {
            if (routeId <= 0) return Task.FromResult(NotFoundOrForbidden());

            return GuardAsync(_logger, "despachar ruta", async () =>
            {
                var recipients = await _management.DispatchRouteAsync(routeId, CurrentUserId, CurrentUserName, cancellationToken);

                await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                    "Despachar ruta", "Rutas",
                    $"Se despachó la ruta #{routeId}. {recipients.Count} pedido(s) pasaron a En ruta.",
                    ClientIp, ClientUserAgent, cancellationToken);

                NotifyCustomersInBackground(recipients);

                return Ok(new DispatchRouteResponse { RutaId = routeId, PedidosEnRuta = recipients.Count });
            });
        }

        private void NotifyCustomersInBackground(IReadOnlyList<(int PedidoId, string Cliente, string Correo)> recipients)
        {
            if (recipients.Count == 0) return;

            _ = Task.Run(() =>
            {
                using var scope = _scopeFactory.CreateScope();
                var email = scope.ServiceProvider.GetRequiredService<EmailService>();

                foreach (var (orderId, name, address) in recipients)
                {
                    if (string.IsNullOrWhiteSpace(address)) continue;
                    try
                    {
                        email.SendEmail(
                            address,
                            $"Actualización de su pedido #{orderId} - En ruta",
                            $"<p>Hola {WebUtility.HtmlEncode(name)},</p>" +
                            $"<p>Le informamos que su pedido <strong>#{orderId}</strong> ahora está <strong>en ruta</strong>.</p>" +
                            "<p>Gracias por su preferencia.<br/>Distribuidora JJ</p>");
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "No se pudo avisar al cliente del pedido #{PedidoId}.", orderId);
                    }
                }
            });
        }

        private DateTime CostaRicaToday()
        {
            // Costa Rica no tiene horario de verano: UTC-6 todo el año. El App
            // Service corre en UTC, y "hoy" a las 7 p. m. en San José ya es
            // mañana en UTC.
            return _time.GetUtcNow().ToOffset(TimeSpan.FromHours(-6)).Date;
        }
    }
}
