namespace Proyecto_Final.Services
{
    /// <summary>
    /// IVA del pedido (13 %, sin productos exentos): la misma regla y el mismo redondeo que usan la factura
    /// (<c>sp_Admin_GenerateInvoiceFromOrder</c>: <c>ROUND(Subtotal * 0.13, 2)</c>) y los procedimientos que crean
    /// pedidos desde 0330. Solo sirve para MOSTRAR el carrito antes de confirmar; el pedido guarda lo que calcula
    /// la base de datos.
    /// </summary>
    public static class OrderTax
    {
        public const decimal Rate = 0.13m;

        /// <summary>Texto para etiquetas: «IVA (13 %)».</summary>
        public const string Label = "IVA (13 %)";

        /// <summary>IVA de un subtotal (ya con los descuentos), redondeado a 2 decimales como SQL Server (mitad hacia arriba).</summary>
        public static decimal Calculate(decimal subtotal) =>
            Math.Round(subtotal * Rate, 2, MidpointRounding.AwayFromZero);
    }
}
