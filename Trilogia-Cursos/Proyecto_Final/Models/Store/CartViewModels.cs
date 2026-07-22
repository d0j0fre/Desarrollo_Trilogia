using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Store
{
    public class CartItemViewModel
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public int StockDisponible { get; set; }
        public int Cantidad { get; set; }
        public string ImagenUrl { get; set; } = "~/img/product-1.jpg";
        public decimal Subtotal => Precio * Cantidad;

        // CU-173 — promociones aplicadas a la línea (calculadas, no persistidas en sesión).
        public decimal MontoDescuento { get; set; }
        public bool EsRegalo { get; set; }
        public string? PromocionNombre { get; set; }
        public decimal SubtotalConDescuento => Subtotal - MontoDescuento;
    }

    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();
        public int TotalItems => Items.Sum(x => x.Cantidad);
        public decimal Subtotal => Items.Sum(x => x.Subtotal);

        // CU-173 — resultado del motor de promociones.
        public List<CartItemViewModel> Regalias { get; set; } = new();
        public decimal DescuentoTotal => Items.Sum(x => x.MontoDescuento);
        public decimal Total => Subtotal - DescuentoTotal;
        public bool TienePromociones => DescuentoTotal > 0 || Regalias.Count > 0;
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

    public class CheckoutViewModel
    {
        [Display(Name = "Tipo de entrega")]
        public string TipoEntrega { get; set; } = "Envío a domicilio";

        [Display(Name = "Dirección de entrega")]
        [StringLength(500)]
        public string? DireccionEntrega { get; set; }

        [Display(Name = "Observaciones")]
        [StringLength(300)]
        public string? Observaciones { get; set; }

        [Display(Name = "Correo electrónico")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        public string? CorreoElectronico { get; set; }

        [Display(Name = "Tipo de cliente")]
        public string TipoCliente { get; set; } = "Cliente Físico";

        [Display(Name = "Identificación")]
        [Required(ErrorMessage = "La identificación es obligatoria.")]
        [StringLength(50)]
        public string? Identificacion { get; set; }

        [Display(Name = "Factura electrónica")]
        public bool FacturaElectronica { get; set; }

        [Display(Name = "Teléfono")]
        [Phone]
        public string? Telefono { get; set; }

        [Display(Name = "Teléfono 2")]
        [Phone]
        public string? Telefono2 { get; set; }

        [Display(Name = "País / Región")]
        public string Pais { get; set; } = "Costa Rica";

        [Display(Name = "Provincia")]
        public string? Provincia { get; set; }

        [Display(Name = "Cantón")]
        public string? Canton { get; set; }

        [Display(Name = "Distrito")]
        public string? Distrito { get; set; }

        [Display(Name = "Dirección escrita o señas")]
        public string? DireccionDetalle { get; set; }

        [Display(Name = "Método de pago")]
        public string MetodoPago { get; set; } = "Efectivo contra entrega";

        [Display(Name = "Referencia de pago")]
        [StringLength(80, ErrorMessage = "La referencia no puede superar 80 caracteres.")]
        public string? ReferenciaPago { get; set; }

        public CartViewModel Cart { get; set; } = new();
    }

    public class OrderConfirmationViewModel
    {
        public int PedidoId { get; set; }
        public string TipoEntrega { get; set; } = string.Empty;
        public string? DireccionEntrega { get; set; }
        public decimal Total { get; set; }
        public List<CartItemViewModel> Items { get; set; } = new();
    }

    public sealed class OrderCreationResult
    {
        public int PedidoId { get; set; }
        public decimal Total { get; set; }
        public List<CartItemViewModel> Gifts { get; set; } = new();
    }
}
