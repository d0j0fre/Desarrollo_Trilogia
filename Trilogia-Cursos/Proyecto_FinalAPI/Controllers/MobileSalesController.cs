using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Venta en campo. Usa <c>sp_Seller_CreateOrder</c>, el mismo procedimiento
    /// de la venta móvil del sitio web: umbral de retención, descuento de
    /// inventario y facturación automática quedan idénticos.
    ///
    /// El vendedor que registra sale del token, nunca del cuerpo.
    /// </summary>
    [Route("api/mobile/v1/sales")]
    [Authorize]
    [RequirePermission(MobilePermissions.Access)]
    public sealed class MobileSalesController : MobileControllerBase
    {
        /// <summary>Las mismas opciones del formulario web de venta móvil.</summary>
        public static readonly IReadOnlySet<string> DeliveryTypes =
            new HashSet<string>(StringComparer.Ordinal) { "Entrega por vendedor", "Retiro en local", "Envío a domicilio" };

        private const int MaxLines = 60;

        private readonly IOrdersMobileDbService _orders;
        private readonly IMobileAuditService _audit;
        private readonly ILogger<MobileSalesController> _logger;

        public MobileSalesController(
            IOrdersMobileDbService orders,
            IMobileAuditService audit,
            ILogger<MobileSalesController> logger)
        {
            _orders = orders;
            _audit = audit;
            _logger = logger;
        }

        [HttpGet("clients")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.SellerOrders)]
        public Task<IActionResult> Clients([FromQuery] string? buscar, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "buscar clientes", async () =>
                Ok(await _orders.GetSaleClientsAsync(buscar, cancellationToken)));

        [HttpGet("products")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.SellerOrders)]
        public Task<IActionResult> Products([FromQuery] string? buscar, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "buscar productos para venta", async () =>
                Ok(await _orders.GetSaleProductsAsync(buscar, cancellationToken)));

        [HttpGet("orders")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.SellerOrders)]
        public Task<IActionResult> MyOrders(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar ventas propias", async () =>
                Ok(await _orders.GetSellerOrdersAsync(CurrentUserId, cancellationToken)));

        /// <summary>
        /// Registra la venta. Idempotente por <c>syncGuid</c>, que viaja como
        /// <c>PedidoOfflineGuid</c>: si la cola reintenta, la base devuelve el
        /// pedido ya creado en vez de crear otro.
        /// </summary>
        [HttpPost("orders")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.SellerOrders)]
        public async Task<IActionResult> Create([FromBody] CreateSaleRequest request, CancellationToken cancellationToken)
        {
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (request.ClienteId <= 0) return InvalidRequest("Elegí el cliente.");
            if (!DeliveryTypes.Contains(request.TipoEntrega?.Trim() ?? string.Empty))
                return InvalidRequest("El tipo de entrega no es válido.");
            if (string.IsNullOrWhiteSpace(request.DireccionEntrega))
                return InvalidRequest("Indicá la dirección de entrega.");

            var items = (request.Items ?? new List<CreateSaleItem>())
                .Where(item => item.ProductoId > 0 && item.Cantidad > 0)
                .ToList();
            if (items.Count == 0) return InvalidRequest("Agregá al menos un producto.");
            if (items.Count > MaxLines) return InvalidRequest($"Un pedido admite hasta {MaxLines} productos.");
            if (items.Any(item => item.Cantidad > 10_000)) return InvalidRequest("Una de las cantidades no es válida.");
            if (!TryReadSyncGuid(request.SyncGuid, out var syncGuid))
                return InvalidRequest("Falta un identificador de sincronización válido.");

            request.Items = items;

            return await GuardAsync(_logger, "registrar venta en campo", async () =>
            {
                var result = await _orders.CreateSaleAsync(request, syncGuid, CurrentUserId, CurrentUserName, cancellationToken);
                if (result is null) return Unavailable();

                await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                    result.Retenido ? "Registrar pedido retenido" : "Registrar pedido móvil",
                    "Venta móvil",
                    $"{CurrentUserName} registró el pedido #{result.PedidoId} para el cliente #{request.ClienteId}. Estado: {result.Estado}.",
                    ClientIp, ClientUserAgent, cancellationToken);

                return Ok(result);
            });
        }
    }
}
