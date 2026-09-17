using System.Data;
using System.Text.Json;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public interface IOrdersMobileDbService
    {
        Task<IReadOnlyList<MobileOrderSummary>> GetOrdersAsync(string? status, string? search, CancellationToken cancellationToken = default);
        Task<MobileOrderDetail?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);
        Task ChangeStatusAsync(int orderId, string newStatus, int userId, string userName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobilePickingOrder>> GetPickingAsync(bool prepared, CancellationToken cancellationToken = default);
        Task<MarkPreparedResponse?> MarkPreparedAsync(int orderId, string? notes, Guid syncGuid, int userId, string userName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileRetainedOrder>> GetRetainedAsync(string? search, CancellationToken cancellationToken = default);
        Task<OrderDecisionResponse?> ApproveRetainedAsync(int orderId, int userId, string userName, CancellationToken cancellationToken = default);
        Task<OrderDecisionResponse?> RejectRetainedAsync(int orderId, string reason, int userId, string userName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileSaleClient>> GetSaleClientsAsync(string? search, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileSaleProduct>> GetSaleProductsAsync(string? search, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileSellerOrder>> GetSellerOrdersAsync(int sellerUserId, CancellationToken cancellationToken = default);
        Task<CreateSaleResponse?> CreateSaleAsync(CreateSaleRequest request, Guid syncGuid, int sellerUserId, string sellerName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Pedidos: consulta y cambio de estado (gestión), preparación (bodega),
    /// aprobación de retenidos (gerencia) y venta en campo (vendedor).
    /// </summary>
    public sealed class OrdersMobileDbService : MobileDbServiceBase, IOrdersMobileDbService
    {
        /// <summary>Canal que CLAUDE.md fija para los pedidos tomados sin señal.</summary>
        public const string OfflineChannel = "Venta móvil offline";
        public const string OnlineChannel = "Venta móvil";

        public OrdersMobileDbService(IConfiguration configuration) : base(configuration) { }

        public async Task<IReadOnlyList<MobileOrderSummary>> GetOrdersAsync(
            string? status, string? search, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Movil_Pedidos_Listar", p =>
            {
                p.Add("@Estado", SqlDbType.NVarChar, 30).Value = DbText(status, 30);
                p.Add("@Buscar", SqlDbType.NVarChar, 150).Value = DbText(search, 150);
                p.Add("@Top", SqlDbType.Int).Value = 100;
            }, r => new MobileOrderSummary
            {
                PedidoId = r.Int("PedidoId"),
                Cliente = r.Str("Cliente"),
                FechaPedido = r.Date("FechaPedido"),
                Estado = r.Str("Estado"),
                TipoEntrega = r.Str("TipoEntrega"),
                Total = r.Dec("Total"),
                VendedorNombre = r.Str("VendedorNombre"),
                CanalPedido = r.Str("CanalPedido"),
                TieneFactura = r.Bool("TieneFactura"),
                Preparado = r.Bool("Preparado")
            }, cancellationToken);

        public async Task<MobileOrderDetail?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default)
        {
            MobileOrderDetail? order = null;

            await ReadAsync("dbo.sp_Movil_Pedido_Detalle",
                p => p.Add("@PedidoId", SqlDbType.Int).Value = orderId,
                async r =>
                {
                    if (!await r.ReadAsync(cancellationToken)) return;

                    order = new MobileOrderDetail
                    {
                        PedidoId = r.Int("PedidoId"),
                        Cliente = r.Str("Cliente"),
                        ClienteCorreo = r.Str("ClienteCorreo"),
                        ClienteTelefono = r.Str("ClienteTelefono"),
                        FechaPedido = r.Date("FechaPedido"),
                        Estado = r.Str("Estado"),
                        TipoEntrega = r.Str("TipoEntrega"),
                        DireccionEntrega = r.Str("DireccionEntrega"),
                        Total = r.Dec("Total"),
                        Observaciones = r.Str("Observaciones"),
                        VendedorNombre = r.Str("VendedorNombre"),
                        CanalPedido = r.Str("CanalPedido"),
                        MotivoRechazo = r.Str("MotivoRechazo"),
                        NumeroFactura = r.Str("NumeroFactura"),
                        TieneFactura = r.Bool("TieneFactura"),
                        PreparadoPorNombre = r.Str("PreparadoPorNombre"),
                        FechaPreparacion = r.Date("FechaPreparacion"),
                        RutaCodigo = r.Str("RutaCodigo"),
                        EstadoEntrega = r.Str("EstadoEntrega")
                    };
                    order.TransicionesPermitidas = OrderStatusRules.AllowedFrom(order.Estado, order.TieneFactura).ToList();

                    if (!await r.NextResultAsync(cancellationToken)) return;
                    while (await r.ReadAsync(cancellationToken))
                    {
                        order.Lineas.Add(new MobileOrderLine
                        {
                            Nombre = r.Str("Nombre"),
                            ProductoId = r.Int("ProductoId"),
                            Cantidad = r.Int("Cantidad"),
                            PrecioUnitario = r.Dec("PrecioUnitario"),
                            Subtotal = r.Dec("Subtotal"),
                            StockActual = r.Int("StockActual"),
                            EsCombo = r.Bool("EsCombo")
                        });
                    }
                }, cancellationToken);

            return order;
        }

        public Task ChangeStatusAsync(
            int orderId, string newStatus, int userId, string userName, CancellationToken cancellationToken = default) =>
            ExecuteAsync("dbo.sp_Admin_UpdateOrderStatus", p =>
            {
                p.Add("@PedidoId", SqlDbType.Int).Value = orderId;
                p.Add("@NuevoEstado", SqlDbType.NVarChar, 50).Value = newStatus;
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
            }, cancellationToken);

        public async Task<IReadOnlyList<MobilePickingOrder>> GetPickingAsync(
            bool prepared, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Movil_Bodega_PedidosPorPreparar",
                p => p.Add("@Preparados", SqlDbType.Bit).Value = prepared,
                r => new MobilePickingOrder
                {
                    PedidoId = r.Int("PedidoId"),
                    Cliente = r.Str("Cliente"),
                    FechaPedido = r.Date("FechaPedido"),
                    Estado = r.Str("Estado"),
                    TipoEntrega = r.Str("TipoEntrega"),
                    DireccionEntrega = r.Str("DireccionEntrega"),
                    TotalLineas = r.Int("TotalLineas"),
                    TotalUnidades = r.Int("TotalUnidades"),
                    PreparadoPorNombre = r.Str("PreparadoPorNombre"),
                    FechaPreparacion = r.Date("FechaPreparacion"),
                    RutaCodigo = r.Str("RutaCodigo")
                }, cancellationToken);

        public Task<MarkPreparedResponse?> MarkPreparedAsync(
            int orderId, string? notes, Guid syncGuid, int userId, string userName, CancellationToken cancellationToken = default) =>
            QuerySingleAsync("dbo.sp_Movil_Bodega_MarcarPreparado", p =>
            {
                p.Add("@PedidoId", SqlDbType.Int).Value = orderId;
                p.Add("@Observaciones", SqlDbType.NVarChar, 300).Value = DbText(notes, 300);
                p.Add("@SyncGuid", SqlDbType.UniqueIdentifier).Value = syncGuid;
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
            }, r => new MarkPreparedResponse
            {
                PedidoId = r.Int("PedidoId"),
                PreparadoPorNombre = r.Str("PreparadoPorNombre"),
                FechaPreparacion = r.Date("FechaPreparacion"),
                Duplicado = r.Bool("Duplicado")
            }, cancellationToken);

        public async Task<IReadOnlyList<MobileRetainedOrder>> GetRetainedAsync(
            string? search, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Manager_GetRetainedOrders",
                p => p.Add("@Buscar", SqlDbType.NVarChar, 150).Value = DbText(search, 150),
                r => new MobileRetainedOrder
                {
                    PedidoId = r.Int("PedidoId"),
                    Cliente = r.Str("Cliente"),
                    FechaPedido = r.Date("FechaPedido"),
                    VendedorNombre = r.Str("VendedorNombre"),
                    Total = r.Dec("Total"),
                    TipoEntrega = r.Str("TipoEntrega"),
                    TotalLineas = r.Int("TotalLineas")
                }, cancellationToken);

        public Task<OrderDecisionResponse?> ApproveRetainedAsync(
            int orderId, int userId, string userName, CancellationToken cancellationToken = default) =>
            QuerySingleAsync("dbo.sp_Manager_ApproveOrder", p =>
            {
                p.Add("@PedidoId", SqlDbType.Int).Value = orderId;
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
            }, MapDecision, cancellationToken);

        public Task<OrderDecisionResponse?> RejectRetainedAsync(
            int orderId, string reason, int userId, string userName, CancellationToken cancellationToken = default) =>
            QuerySingleAsync("dbo.sp_Manager_RejectOrder", p =>
            {
                p.Add("@PedidoId", SqlDbType.Int).Value = orderId;
                p.Add("@MotivoRechazo", SqlDbType.NVarChar, 500).Value = DbText(reason, 500);
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
            }, MapDecision, cancellationToken);

        public async Task<IReadOnlyList<MobileSaleClient>> GetSaleClientsAsync(
            string? search, CancellationToken cancellationToken = default) =>
            (await QueryAsync("dbo.sp_Seller_GetClientsForOrder",
                p => p.Add("@Buscar", SqlDbType.NVarChar, 150).Value = DbText(search, 150),
                r => new MobileSaleClient
                {
                    ClienteId = r.Int("UsuarioId"),
                    Nombre = r.Str("NombreCompleto"),
                    Correo = r.Str("Correo"),
                    Telefono = r.Str("Telefono"),
                    Direccion = r.Str("Direccion")
                }, cancellationToken)).Take(60).ToList();

        public async Task<IReadOnlyList<MobileSaleProduct>> GetSaleProductsAsync(
            string? search, CancellationToken cancellationToken = default) =>
            (await QueryAsync("dbo.sp_Seller_GetProductsForOrder",
                p => p.Add("@Buscar", SqlDbType.NVarChar, 150).Value = DbText(search, 150),
                r => new MobileSaleProduct
                {
                    ProductoId = r.Int("ProductoId"),
                    Nombre = r.Str("Nombre"),
                    Categoria = r.Str("Categoria"),
                    Precio = r.Dec("Precio"),
                    Stock = r.Int("Stock")
                }, cancellationToken)).Take(200).ToList();

        public async Task<IReadOnlyList<MobileSellerOrder>> GetSellerOrdersAsync(
            int sellerUserId, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Seller_GetMyOrders", p =>
            {
                p.Add("@VendedorUsuarioId", SqlDbType.Int).Value = sellerUserId;
                p.Add("@Top", SqlDbType.Int).Value = 50;
            }, r => new MobileSellerOrder
            {
                PedidoId = r.Int("PedidoId"),
                Cliente = r.Str("Cliente"),
                FechaPedido = r.Date("FechaPedido"),
                Estado = r.Str("Estado"),
                Total = r.Dec("Total"),
                NumeroFactura = r.Str("NumeroFactura")
            }, cancellationToken);

        public Task<CreateSaleResponse?> CreateSaleAsync(
            CreateSaleRequest request, Guid syncGuid, int sellerUserId, string sellerName, CancellationToken cancellationToken = default)
        {
            // Mismo formato que arma AdminDbService.CreateSellerOrderAsync del sitio web.
            var itemsJson = JsonSerializer.Serialize(request.Items
                .Where(item => item.ProductoId > 0 && item.Cantidad > 0)
                .Select(item => new { productoId = item.ProductoId, cantidad = item.Cantidad }));

            return QuerySingleAsync("dbo.sp_Seller_CreateOrder", p =>
            {
                p.Add("@ClienteUsuarioId", SqlDbType.Int).Value = request.ClienteId;
                p.Add("@VendedorUsuarioId", SqlDbType.Int).Value = sellerUserId;
                p.Add("@VendedorNombre", SqlDbType.NVarChar, 150).Value = ActorName(sellerName);
                p.Add("@TipoEntrega", SqlDbType.NVarChar, 100).Value = request.TipoEntrega!.Trim();
                p.Add("@DireccionEntrega", SqlDbType.NVarChar, 500).Value = DbText(request.DireccionEntrega, 500);
                p.Add("@Observaciones", SqlDbType.NVarChar, 500).Value = DbText(request.Observaciones, 500);
                p.Add("@IdentificacionCliente", SqlDbType.NVarChar, 100).Value = DBNull.Value;
                p.Add("@ItemsJson", SqlDbType.NVarChar, -1).Value = itemsJson;
                p.Add("@PedidoOfflineGuid", SqlDbType.UniqueIdentifier).Value = syncGuid;
                p.Add("@CanalPedido", SqlDbType.NVarChar, 50).Value = request.RegistradoSinConexion ? OfflineChannel : OnlineChannel;
                // @UmbralRetencion no se envía: el valor por defecto del
                // procedimiento es el mismo que usa el sitio web.
            }, r =>
            {
                var status = r.Str("Estado");
                return new CreateSaleResponse
                {
                    PedidoId = r.Int("PedidoId"),
                    Estado = status,
                    NumeroFactura = r.Str("NumeroFactura"),
                    Retenido = string.Equals(status, "Retenido", StringComparison.OrdinalIgnoreCase)
                };
            }, cancellationToken);
        }

        private static OrderDecisionResponse MapDecision(Microsoft.Data.SqlClient.SqlDataReader r) => new()
        {
            PedidoId = r.Int("PedidoId"),
            Estado = r.Str("Estado"),
            NumeroFactura = r.Str("NumeroFactura")
        };
    }

    /// <summary>
    /// Espejo de las transiciones que acepta <c>sp_Admin_UpdateOrderStatus</c>.
    /// Solo decide qué botones ofrecer: la base vuelve a validar y es quien manda.
    /// Retenido no se ofrece aquí porque liberar exige facturar e inventario, que
    /// es lo que hace la aprobación de gerencia.
    /// </summary>
    public static class OrderStatusRules
    {
        public static IEnumerable<string> AllowedFrom(string current, bool hasInvoice)
        {
            if (current is "Cancelado" or "Rechazado") return Array.Empty<string>();
            if (hasInvoice) return current == "Entregado" ? Array.Empty<string>() : new[] { "Entregado" };

            return current switch
            {
                "Pendiente" => new[] { "Aprobado", "Cancelado" },
                "Aprobado" => new[] { "EnProceso", "Cancelado" },
                "EnProceso" => new[] { "Entregado", "Cancelado" },
                "Entregado" => new[] { "Cancelado" },
                "Liberado" => new[] { "EnProceso", "Cancelado" },
                _ => Array.Empty<string>()
            };
        }

        public static bool IsAllowed(string current, bool hasInvoice, string target) =>
            AllowedFrom(current, hasInvoice).Contains(target, StringComparer.Ordinal);
    }
}
