using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Inventario y compras desde el teléfono: consulta de existencias,
    /// movimientos manuales, estado de productos y recepción de mercadería.
    ///
    /// Cada endpoint exige el mismo permiso que su pantalla equivalente en el
    /// sitio web. Las escrituras que pueden ir por la cola offline exigen un
    /// identificador de sincronización y son idempotentes en la base.
    /// </summary>
    [Route("api/mobile/v1/inventory")]
    [Authorize]
    [RequirePermission(MobilePermissions.Access)]
    public sealed class MobileInventoryController : MobileControllerBase
    {
        private static readonly HashSet<string> MovementTypes =
            new(StringComparer.Ordinal) { "Entrada", "Salida", "Ajuste" };

        private static readonly HashSet<string> ProductFilters =
            new(StringComparer.OrdinalIgnoreCase) { "Todos", "Bajo", "Agotado", "Inactivos" };

        private readonly IInventoryMobileDbService _inventory;
        private readonly IMobileAuditService _audit;
        private readonly ILogger<MobileInventoryController> _logger;

        public MobileInventoryController(
            IInventoryMobileDbService inventory,
            IMobileAuditService audit,
            ILogger<MobileInventoryController> logger)
        {
            _inventory = inventory;
            _audit = audit;
            _logger = logger;
        }

        // ── Existencias ─────────────────────────────────────────────────────

        [HttpGet("products")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.InventoryView)]
        public Task<IActionResult> Products([FromQuery] string? buscar, [FromQuery] string? filtro, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(filtro) && !ProductFilters.Contains(filtro))
            {
                return Task.FromResult(InvalidRequest("El filtro indicado no es válido."));
            }

            return GuardAsync(_logger, "consultar productos", async () =>
                Ok(await _inventory.GetProductsAsync(buscar, filtro, cancellationToken)));
        }

        [HttpGet("products/{productId:int}/movements")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.InventoryView)]
        public Task<IActionResult> Movements(int productId, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar movimientos", async () =>
                Ok(await _inventory.GetMovementsAsync(productId, cancellationToken)));

        [HttpPost("movements")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.InventoryMovements)]
        public async Task<IActionResult> RegisterMovement(
            [FromBody] RegisterMovementRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (request.ProductoId <= 0) return InvalidRequest("Indicá el producto.");
            if (!MovementTypes.Contains(request.TipoMovimiento ?? string.Empty))
                return InvalidRequest("El tipo de movimiento no es válido.");
            if (request.Cantidad < 0 || request.Cantidad > 1_000_000)
                return InvalidRequest("La cantidad no es válida.");
            if (!TryReadSyncGuid(request.SyncGuid, out var syncGuid))
                return InvalidRequest("Falta un identificador de sincronización válido.");

            return await GuardAsync(_logger, "registrar movimiento de inventario", async () =>
            {
                var result = await _inventory.RegisterMovementAsync(
                    request.ProductoId,
                    request.TipoMovimiento!,
                    request.Cantidad,
                    request.Motivo,
                    syncGuid,
                    CurrentUserId,
                    CurrentUserName,
                    cancellationToken);

                if (result is null) return Unavailable();

                if (!result.Duplicado)
                {
                    await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                        "Registrar movimiento", "Inventario",
                        $"{result.TipoMovimiento} de {request.Cantidad} en {result.ProductoNombre} (#{result.ProductoId}): stock {result.StockAnterior} → {result.StockNuevo}.",
                        ClientIp, ClientUserAgent, cancellationToken);
                }

                return Ok(result);
            });
        }

        [HttpPost("products/{productId:int}/status")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.ProductsEdit)]
        public async Task<IActionResult> ChangeProductStatus(
            int productId,
            [FromBody] ChangeProductStatusRequest request,
            CancellationToken cancellationToken)
        {
            if (productId <= 0) return InvalidRequest("Indicá el producto.");
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (!TryReadSyncGuid(request.SyncGuid, out var syncGuid))
                return InvalidRequest("Falta un identificador de sincronización válido.");

            return await GuardAsync(_logger, "cambiar estado de producto", async () =>
            {
                var result = await _inventory.ChangeProductStatusAsync(productId, request.Activo, syncGuid, CurrentUserId, cancellationToken);
                if (result is null) return Unavailable();

                if (result.Cambio)
                {
                    await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                        result.Activo ? "Activar" : "Inactivar", "Inventario",
                        result.Activo
                            ? $"Se reactivó el producto #{productId} ({result.Nombre})."
                            : $"Se inactivó el producto #{productId} ({result.Nombre}) para ocultarlo del catálogo.",
                        ClientIp, ClientUserAgent, cancellationToken);
                }

                return Ok(result);
            });
        }

        // ── Compras ─────────────────────────────────────────────────────────

        [HttpGet("purchase-orders")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.PurchaseOrdersView)]
        public Task<IActionResult> PurchaseOrders([FromQuery] string? estado, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar órdenes de compra", async () =>
                Ok(await _inventory.GetPurchaseOrdersAsync(estado, cancellationToken)));

        [HttpGet("purchase-orders/{purchaseOrderId:int}")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.PurchaseOrdersView)]
        public Task<IActionResult> PurchaseOrder(int purchaseOrderId, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar orden de compra", async () =>
            {
                var order = await _inventory.GetPurchaseOrderAsync(purchaseOrderId, cancellationToken);
                return order is null ? NotFoundOrForbidden() : Ok(order);
            });

        /// <summary>
        /// Recibe mercadería de una línea. Idempotente por <c>syncGuid</c>: si la
        /// cola reintenta, la base reconoce el token y no suma dos veces.
        /// </summary>
        [HttpPost("purchase-orders/{purchaseOrderId:int}/lines/{lineId:int}/receive")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.ReceivePurchases)]
        public async Task<IActionResult> ReceiveLine(
            int purchaseOrderId,
            int lineId,
            [FromBody] ReceivePurchaseLineRequest request,
            CancellationToken cancellationToken)
        {
            if (purchaseOrderId <= 0 || lineId <= 0) return InvalidRequest("La línea indicada no es válida.");
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (request.Cantidad <= 0 || request.Cantidad > 1_000_000) return InvalidRequest("La cantidad debe ser mayor a cero.");
            if (!TryReadSyncGuid(request.SyncGuid, out var syncGuid))
                return InvalidRequest("Falta un identificador de sincronización válido.");

            return await GuardAsync(_logger, "recibir línea de compra", async () =>
            {
                await _inventory.ReceivePurchaseLineAsync(
                    purchaseOrderId, lineId, request.Cantidad, syncGuid,
                    CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                    ClientIp, ClientUserAgent, cancellationToken);

                // Sin auditoría aparte: sp_Compras_RecibirDetalle ya escribe
                // ComprasAuditoria con usuario, rol e IP, y solo la primera vez.
                // Registrar aquí duplicaría la entrada en cada reintento de la cola.
                return Ok(await _inventory.GetPurchaseOrderAsync(purchaseOrderId, cancellationToken));
            });
        }

        [HttpGet("purchase-suggestions")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.PurchaseSuggestions)]
        public Task<IActionResult> PurchaseSuggestions(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar sugerencias de compra", async () =>
                Ok(await _inventory.GetPurchaseSuggestionsAsync(cancellationToken)));
    }
}
