using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

/// <summary>
/// Venta móvil: quien registra pedidos solo consulta los suyos. «Suyo» es lo que
/// devuelve sp_Seller_GetMyOrders, que filtra por Pedidos.VendedorUsuarioId.
/// </summary>
public interface ISellerOrderAccess
{
    Task<bool> IsOwnOrderAsync(int sellerUserId, int orderId);

    /// <summary>El detalle del pedido si es del vendedor; null si no existe o es de otro.</summary>
    Task<OrderDetailViewModel?> GetOwnOrderAsync(int sellerUserId, int orderId);
}

public sealed class SellerOrderAccess(AdminDbService adminDbService) : ISellerOrderAccess
{
    public async Task<bool> IsOwnOrderAsync(int sellerUserId, int orderId)
    {
        if (sellerUserId <= 0 || orderId <= 0)
        {
            return false;
        }

        var orders = await adminDbService.GetSellerMyOrdersAsync(sellerUserId, int.MaxValue);
        return orders.Any(order => order.PedidoId == orderId);
    }

    public async Task<OrderDetailViewModel?> GetOwnOrderAsync(int sellerUserId, int orderId) =>
        await IsOwnOrderAsync(sellerUserId, orderId)
            ? await adminDbService.GetOrderDetailAsync(orderId)
            : null;
}
