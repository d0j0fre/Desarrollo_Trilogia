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
    }

    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();
        public int TotalItems => Items.Sum(x => x.Cantidad);
        public decimal Subtotal => Items.Sum(x => x.Subtotal);
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
}
