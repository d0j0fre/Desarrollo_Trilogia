namespace Proyecto_Final.Services;

/// <summary>
/// Nombre que se muestra del método de pago de un pedido (G3-19). Los pedidos de
/// Venta móvil contra entrega anteriores a la 0255 quedaron guardados como «Crédito
/// vendedor», que se confunde con un pedido a crédito; los datos no se cambian, solo
/// cómo se muestran.
/// </summary>
public static class PaymentMethodNames
{
    public const string HistoricalSellerCashOnDelivery = "Crédito vendedor";

    public static string Display(string? metodoPago) => metodoPago switch
    {
        null or "" => "No registrado",
        HistoricalSellerCashOnDelivery => "Pago contra entrega",
        _ => metodoPago,
    };
}
