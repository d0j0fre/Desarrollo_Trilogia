using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Pedidos desde el teléfono: consulta y cambio de estado para gestión,
    /// preparación para bodega, y aprobación de retenidos para gerencia.
    /// </summary>
    [Route("api/mobile/v1/orders")]
    [Authorize]
    [RequirePermission(MobilePermissions.Access)]
    public sealed class MobileOrdersController : MobileControllerBase
    {
        private readonly IOrdersMobileDbService _orders;
        private readonly IMobileAuditService _audit;
        private readonly ILogger<MobileOrdersController> _logger;

        public MobileOrdersController(
            IOrdersMobileDbService orders,
            IMobileAuditService audit,
            ILogger<MobileOrdersController> logger)
        {
            _orders = orders;
            _audit = audit;
            _logger = logger;
        }

        // ── Consulta y estado ───────────────────────────────────────────────

        [HttpGet]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.OrdersView)]
        public Task<IActionResult> List([FromQuery] string? estado, [FromQuery] string? buscar, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar pedidos", async () =>
                Ok(await _orders.GetOrdersAsync(estado, buscar, cancellationToken)));

        [HttpGet("{orderId:int}")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.OrdersView)]
        public Task<IActionResult> Detail(int orderId, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar pedido", async () =>
            {
                var order = await _orders.GetOrderAsync(orderId, cancellationToken);
                return order is null ? NotFoundOrForbidden() : Ok(order);
            });

        /// <summary>
        /// Cambia el estado con las mismas transiciones del sitio web. Se valida
        /// antes contra el estado actual para responder un mensaje claro; la base
        /// vuelve a validar dentro de su transacción.
        /// </summary>
        [HttpPost("{orderId:int}/status")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.OrdersChangeStatus)]
        public async Task<IActionResult> ChangeStatus(
            int orderId,
            [FromBody] ChangeOrderStatusRequest request,
            CancellationToken cancellationToken)
        {
            if (orderId <= 0) return NotFoundOrForbidden();
            var target = request?.Estado?.Trim();
            if (string.IsNullOrWhiteSpace(target)) return InvalidRequest("Indicá el estado nuevo.");

            return await GuardAsync(_logger, "cambiar estado de pedido", async () =>
            {
                var order = await _orders.GetOrderAsync(orderId, cancellationToken);
                if (order is null) return NotFoundOrForbidden();

                if (string.Equals(order.Estado, target, StringComparison.Ordinal)) return Ok(order);

                if (!OrderStatusRules.IsAllowed(order.Estado, order.TieneFactura, target))
                {
                    return BusinessRule(order.TieneFactura
                        ? "Un pedido facturado solo puede marcarse como entregado."
                        : $"Un pedido en estado {order.Estado} no puede pasar a {target}.");
                }

                await _orders.ChangeStatusAsync(orderId, target, CurrentUserId, CurrentUserName, cancellationToken);

                await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                    "Cambiar estado de pedido", "Pedidos",
                    $"Pedido #{orderId}: {order.Estado} → {target}.",
                    ClientIp, ClientUserAgent, cancellationToken);

                return Ok(await _orders.GetOrderAsync(orderId, cancellationToken));
            });
        }

        // ── Bodega ──────────────────────────────────────────────────────────

        [HttpGet("picking")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.PrepareOrders)]
        public Task<IActionResult> Picking([FromQuery] bool preparados, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar pedidos por preparar", async () =>
                Ok(await _orders.GetPickingAsync(preparados, cancellationToken)));

        [HttpPost("{orderId:int}/prepared")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.PrepareOrders)]
        public async Task<IActionResult> MarkPrepared(
            int orderId,
            [FromBody] MarkPreparedRequest request,
            CancellationToken cancellationToken)
        {
            if (orderId <= 0) return NotFoundOrForbidden();
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (!TryReadSyncGuid(request.SyncGuid, out var syncGuid))
                return InvalidRequest("Falta un identificador de sincronización válido.");

            return await GuardAsync(_logger, "marcar pedido preparado", async () =>
            {
                var result = await _orders.MarkPreparedAsync(orderId, request.Observaciones, syncGuid, CurrentUserId, CurrentUserName, cancellationToken);
                if (result is null) return Unavailable();

                if (!result.Duplicado)
                {
                    await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                        "Preparar pedido", "Pedidos",
                        $"Pedido #{orderId} preparado en bodega.",
                        ClientIp, ClientUserAgent, cancellationToken);
                }

                return Ok(result);
            });
        }

        // ── Gerencia ────────────────────────────────────────────────────────

        [HttpGet("retained")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.AuthorizeOrders)]
        public Task<IActionResult> Retained([FromQuery] string? buscar, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar pedidos retenidos", async () =>
                Ok(await _orders.GetRetainedAsync(buscar, cancellationToken)));

        /// <summary>
        /// Aprueba un pedido retenido: descuenta inventario y genera la factura en
        /// la misma transacción del procedimiento del sitio web. No va por la cola
        /// offline: aprobar sin ver el stock actual no es una decisión informada.
        /// </summary>
        [HttpPost("{orderId:int}/approve")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.AuthorizeOrders)]
        public Task<IActionResult> Approve(int orderId, CancellationToken cancellationToken)
        {
            if (orderId <= 0) return Task.FromResult(NotFoundOrForbidden());

            return GuardAsync(_logger, "aprobar pedido retenido", async () =>
            {
                var result = await _orders.ApproveRetainedAsync(orderId, CurrentUserId, CurrentUserName, cancellationToken);
                if (result is null) return Unavailable();

                await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                    "Aprobar pedido retenido", "Pedidos",
                    $"{CurrentUserName} aprobó el pedido #{orderId}. Factura generada: {result.NumeroFactura}.",
                    ClientIp, ClientUserAgent, cancellationToken);

                return Ok(result);
            });
        }

        [HttpPost("{orderId:int}/reject")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.AuthorizeOrders)]
        public Task<IActionResult> Reject(int orderId, [FromBody] RejectOrderRequest request, CancellationToken cancellationToken)
        {
            if (orderId <= 0) return Task.FromResult(NotFoundOrForbidden());
            var reason = request?.Motivo?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
                return Task.FromResult(InvalidRequest("Explicá el motivo del rechazo (al menos 5 caracteres)."));

            return GuardAsync(_logger, "rechazar pedido retenido", async () =>
            {
                var result = await _orders.RejectRetainedAsync(orderId, reason, CurrentUserId, CurrentUserName, cancellationToken);
                if (result is null) return Unavailable();

                await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                    "Rechazar pedido retenido", "Pedidos",
                    $"{CurrentUserName} rechazó el pedido #{orderId}. Motivo: {reason}.",
                    ClientIp, ClientUserAgent, cancellationToken);

                return Ok(result);
            });
        }
    }
}
