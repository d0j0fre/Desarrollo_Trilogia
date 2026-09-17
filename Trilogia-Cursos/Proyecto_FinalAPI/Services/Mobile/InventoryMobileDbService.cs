using System.Data;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public interface IInventoryMobileDbService
    {
        Task<IReadOnlyList<MobileProduct>> GetProductsAsync(string? search, string? filter, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileInventoryMovement>> GetMovementsAsync(int productId, CancellationToken cancellationToken = default);
        Task<RegisterMovementResponse?> RegisterMovementAsync(int productId, string type, int quantity, string? reason, Guid syncGuid, int userId, string userName, CancellationToken cancellationToken = default);
        Task<ChangeProductStatusResponse?> ChangeProductStatusAsync(int productId, bool active, Guid syncGuid, int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobilePurchaseOrder>> GetPurchaseOrdersAsync(string? status, CancellationToken cancellationToken = default);
        Task<MobilePurchaseOrderDetail?> GetPurchaseOrderAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
        Task ReceivePurchaseLineAsync(int purchaseOrderId, int lineId, int quantity, Guid syncGuid, int userId, string userName, string userEmail, string role, string? ip, string? userAgent, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobilePurchaseSuggestion>> GetPurchaseSuggestionsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Inventario y compras para bodega, gerencia y compras.
    /// Movimientos y estado de producto usan procedimientos móviles idempotentes
    /// (0028); la recepción de compras reutiliza el procedimiento del sitio web,
    /// que ya era idempotente por token de operación.
    /// </summary>
    public sealed class InventoryMobileDbService : MobileDbServiceBase, IInventoryMobileDbService
    {
        public InventoryMobileDbService(IConfiguration configuration) : base(configuration) { }

        public async Task<IReadOnlyList<MobileProduct>> GetProductsAsync(
            string? search, string? filter, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Movil_Productos_Listar", p =>
            {
                p.Add("@Buscar", SqlDbType.NVarChar, 150).Value = DbText(search, 150);
                p.Add("@Filtro", SqlDbType.NVarChar, 20).Value = DbText(filter, 20);
                p.Add("@Top", SqlDbType.Int).Value = 200;
            }, r => new MobileProduct
            {
                ProductoId = r.Int("ProductoId"),
                Nombre = r.Str("Nombre"),
                Categoria = r.Str("Categoria"),
                Precio = r.Dec("Precio"),
                Stock = r.Int("Stock"),
                StockMinimo = r.Int("StockMinimo"),
                EstadoStock = r.Str("EstadoStock"),
                Activo = r.Bool("Activo")
            }, cancellationToken);

        public async Task<IReadOnlyList<MobileInventoryMovement>> GetMovementsAsync(
            int productId, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Movil_Inventario_MovimientosProducto", p =>
            {
                p.Add("@ProductoId", SqlDbType.Int).Value = productId;
                p.Add("@Top", SqlDbType.Int).Value = 20;
            }, r => new MobileInventoryMovement
            {
                MovimientoId = r.Int("MovimientoId"),
                TipoMovimiento = r.Str("TipoMovimiento"),
                Cantidad = r.Int("Cantidad"),
                StockAnterior = r.Int("StockAnterior"),
                StockNuevo = r.Int("StockNuevo"),
                Motivo = r.Str("Motivo"),
                UsuarioNombre = r.Str("UsuarioNombre"),
                FechaMovimiento = r.Date("FechaMovimiento")
            }, cancellationToken);

        public Task<RegisterMovementResponse?> RegisterMovementAsync(
            int productId, string type, int quantity, string? reason, Guid syncGuid,
            int userId, string userName, CancellationToken cancellationToken = default) =>
            QuerySingleAsync("dbo.sp_Movil_Inventario_RegistrarMovimiento", p =>
            {
                p.Add("@ProductoId", SqlDbType.Int).Value = productId;
                p.Add("@TipoMovimiento", SqlDbType.NVarChar, 20).Value = type;
                p.Add("@Cantidad", SqlDbType.Int).Value = quantity;
                p.Add("@Motivo", SqlDbType.NVarChar, 300).Value = DbText(reason, 300);
                p.Add("@SyncGuid", SqlDbType.UniqueIdentifier).Value = syncGuid;
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
            }, r => new RegisterMovementResponse
            {
                ProductoId = r.Int("ProductoId"),
                ProductoNombre = r.Str("ProductoNombre"),
                TipoMovimiento = r.Str("TipoMovimiento"),
                StockAnterior = r.Int("StockAnterior"),
                StockNuevo = r.Int("StockNuevo"),
                Duplicado = r.Bool("Duplicado")
            }, cancellationToken);

        public Task<ChangeProductStatusResponse?> ChangeProductStatusAsync(
            int productId, bool active, Guid syncGuid, int userId, CancellationToken cancellationToken = default) =>
            QuerySingleAsync("dbo.sp_Movil_Producto_CambiarEstado", p =>
            {
                p.Add("@ProductoId", SqlDbType.Int).Value = productId;
                p.Add("@Activo", SqlDbType.Bit).Value = active;
                p.Add("@SyncGuid", SqlDbType.UniqueIdentifier).Value = syncGuid;
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
            }, r => new ChangeProductStatusResponse
            {
                ProductoId = r.Int("ProductoId"),
                Nombre = r.Str("Nombre"),
                Activo = r.Bool("Activo"),
                Cambio = r.Bool("Cambio")
            }, cancellationToken);

        public async Task<IReadOnlyList<MobilePurchaseOrder>> GetPurchaseOrdersAsync(
            string? status, CancellationToken cancellationToken = default)
        {
            static MobilePurchaseOrder Map(Microsoft.Data.SqlClient.SqlDataReader r) => new()
            {
                OrdenCompraId = r.Int("OrdenCompraId"),
                ProveedorNombre = r.Str("ProveedorNombre"),
                Estado = r.Str("Estado"),
                Notas = r.Str("Notas"),
                FechaCreacion = r.Date("FechaCreacionUtc"),
                MontoTotal = r.Dec("MontoTotal"),
                TotalOrdenado = r.Int("TotalOrdenado"),
                TotalRecibido = r.Int("TotalRecibido")
            };

            // "Abiertas" es lo que bodega necesita ver: lo que falta por recibir.
            if (string.Equals(status, "Abiertas", StringComparison.OrdinalIgnoreCase))
            {
                var pending = await QueryAsync("dbo.sp_Compras_ListarOrdenes", p =>
                {
                    p.Add("@Estado", SqlDbType.NVarChar, 30).Value = "Pendiente";
                    p.Add("@ProveedorId", SqlDbType.Int).Value = DBNull.Value;
                }, Map, cancellationToken);
                var partial = await QueryAsync("dbo.sp_Compras_ListarOrdenes", p =>
                {
                    p.Add("@Estado", SqlDbType.NVarChar, 30).Value = "RecibidaParcial";
                    p.Add("@ProveedorId", SqlDbType.Int).Value = DBNull.Value;
                }, Map, cancellationToken);

                return partial.Concat(pending).OrderByDescending(o => o.FechaCreacion).ToList();
            }

            var orders = await QueryAsync("dbo.sp_Compras_ListarOrdenes", p =>
            {
                p.Add("@Estado", SqlDbType.NVarChar, 30).Value = DbText(status, 30);
                p.Add("@ProveedorId", SqlDbType.Int).Value = DBNull.Value;
            }, Map, cancellationToken);

            return orders.Take(100).ToList();
        }

        public async Task<MobilePurchaseOrderDetail?> GetPurchaseOrderAsync(
            int purchaseOrderId, CancellationToken cancellationToken = default)
        {
            MobilePurchaseOrderDetail? detail = null;

            await ReadAsync("dbo.sp_Compras_ObtenerOrdenDetalle",
                p => p.Add("@OrdenCompraId", SqlDbType.Int).Value = purchaseOrderId,
                async r =>
                {
                    if (!await r.ReadAsync(cancellationToken)) return;

                    var status = r.Str("Estado");
                    detail = new MobilePurchaseOrderDetail
                    {
                        OrdenCompraId = r.Int("OrdenCompraId"),
                        ProveedorNombre = r.Str("ProveedorNombre"),
                        Estado = status,
                        Notas = r.Str("Notas"),
                        FechaCreacion = r.Date("FechaCreacionUtc"),
                        AdmiteRecepcion = status is "Pendiente" or "RecibidaParcial"
                    };

                    if (!await r.NextResultAsync(cancellationToken)) return;
                    while (await r.ReadAsync(cancellationToken))
                    {
                        detail.Lineas.Add(new MobilePurchaseOrderLine
                        {
                            DetalleOrdenCompraId = r.Int("DetalleOrdenCompraId"),
                            ProductoId = r.Int("ProductoId"),
                            ProductoNombre = r.Str("ProductoNombre"),
                            CantidadOrdenada = r.Int("CantidadOrdenada"),
                            CantidadRecibida = r.Int("CantidadRecibida"),
                            PrecioUnitario = r.Dec("PrecioUnitario")
                        });
                    }
                }, cancellationToken);

            return detail;
        }

        public Task ReceivePurchaseLineAsync(
            int purchaseOrderId, int lineId, int quantity, Guid syncGuid, int userId, string userName,
            string userEmail, string role, string? ip, string? userAgent, CancellationToken cancellationToken = default) =>
            ExecuteAsync("dbo.sp_Compras_RecibirDetalle", p =>
            {
                p.Add("@OrdenCompraId", SqlDbType.Int).Value = purchaseOrderId;
                p.Add("@DetalleOrdenCompraId", SqlDbType.Int).Value = lineId;
                p.Add("@CantidadRecibidaAhora", SqlDbType.Int).Value = quantity;
                // El token del sitio web cumple el mismo papel que el SyncGuid de
                // la aplicación: un reintento con el mismo token no suma dos veces.
                p.Add("@TokenOperacion", SqlDbType.UniqueIdentifier).Value = syncGuid;
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
                p.Add("@UsuarioCorreo", SqlDbType.NVarChar, 150).Value = DbText(userEmail, 150);
                p.Add("@Rol", SqlDbType.NVarChar, 50).Value = DbText(role, 50);
                p.Add("@DireccionIp", SqlDbType.NVarChar, 80).Value = DbText(ip, 80);
                p.Add("@UserAgent", SqlDbType.NVarChar, 300).Value = DbText(userAgent, 300);
            }, cancellationToken);

        public async Task<IReadOnlyList<MobilePurchaseSuggestion>> GetPurchaseSuggestionsAsync(
            CancellationToken cancellationToken = default) =>
            (await QueryAsync("dbo.sp_Admin_GetPurchaseSuggestions", p =>
            {
                p.Add("@MesesRecientes", SqlDbType.Int).Value = 3;
                p.Add("@MesesCobertura", SqlDbType.Int).Value = 2;
            }, r => new MobilePurchaseSuggestion
            {
                ProductoId = r.Int("ProductoId"),
                Nombre = r.Str("Nombre"),
                StockActual = r.Int("StockActual"),
                StockMinimo = r.Int("StockMinimo"),
                PromedioVentaMensual = r.Dec("PromedioVentaMensual"),
                CantidadSugerida = r.Int("CantidadSugerida"),
                DatosInsuficientes = r.Bool("DatosInsuficientes")
            }, cancellationToken)).Take(100).ToList();
    }
}
