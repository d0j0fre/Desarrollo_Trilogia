namespace Proyecto_Final.Models.Store
{
    public static class CartItemTypes
    {
        public const string Product = "Producto";
        public const string Combo = "Combo";
    }

    public class CartItemViewModel
    {
        public string ItemType { get; set; } = CartItemTypes.Product;
        public int ProductoId { get; set; }
        public int? ComboId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public int StockDisponible { get; set; }
        public int Cantidad { get; set; }
        public string ImagenUrl { get; set; } = "~/img/product-1.jpg";
        // El checkout confirmado puede conservar un subtotal bruto exacto aunque el precio
        // persistido ya incluya un descuento distribuido por unidad.
        public decimal? SubtotalAntesDescuento { get; set; }
        public decimal Subtotal => SubtotalAntesDescuento ?? Precio * Cantidad;

        // CU-173 — promociones aplicadas a la línea (calculadas, no persistidas en sesión).
        public decimal MontoDescuento { get; set; }
        public bool EsRegalo { get; set; }
        public string? PromocionNombre { get; set; }
        public decimal SubtotalConDescuento => EsRegalo ? 0 : Math.Max(0, Subtotal - MontoDescuento);
        public string CartKey => ItemType == CartItemTypes.Combo
            ? $"combo:{ComboId.GetValueOrDefault()}"
            : $"product:{ProductoId}";
    }

    // CU-173 — registro de promoción aplicada, para persistir en el pedido.
    public class AppliedPromotion
    {
        public int PromocionId { get; set; }
        public int ProductoId { get; set; }
        public string TipoBeneficio { get; set; } = string.Empty; // Descuento | Regalia
        public decimal? MontoDescontado { get; set; }
        public int? UnidadesRegalo { get; set; }
        public int? ProductoRegaloId { get; set; }
    }

}
