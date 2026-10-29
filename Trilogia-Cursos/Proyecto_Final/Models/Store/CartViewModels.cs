using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Validation;

namespace Proyecto_Final.Models.Store
{
    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();
        public int TotalItems => Items.Sum(x => x.Cantidad);
        public decimal Subtotal => Items.Sum(x => x.Subtotal);

        // CU-173 — resultado del motor de promociones.
        public List<CartItemViewModel> Regalias { get; set; } = new();
        public IReadOnlyList<CrossSellSuggestion> Recomendaciones { get; set; } = [];
        public decimal DescuentoTotal => Items.Sum(x => x.MontoDescuento);
        public decimal Total => Subtotal - DescuentoTotal;
        public decimal InvoiceTaxEstimate => Math.Round(Total * 0.13m, 2, MidpointRounding.AwayFromZero);
        public decimal InvoiceTotalEstimate => Total + InvoiceTaxEstimate;
        public bool TienePromociones => DescuentoTotal > 0 || Regalias.Count > 0;
    }

    public class CheckoutViewModel
    {
        public Guid OperationToken { get; set; }

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

        [Display(Name = "Identificación")]
        [Required(ErrorMessage = "La identificación es obligatoria.")]
        [StringLength(50)]
        [CostaRicanIdentification]
        public string? Identificacion { get; set; }

        [Display(Name = "Teléfono")]
        [Phone]
        public string? Telefono { get; set; }

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
        public Guid OwnerLoginId { get; set; }
        public int OwnerUserId { get; set; }
        public int PedidoId { get; set; }
        public string TipoEntrega { get; set; } = string.Empty;
        public string? DireccionEntrega { get; set; }
        public decimal Total { get; set; }
        public OrderAmountSnapshot? Amounts { get; set; }
        public List<CartItemViewModel> Items { get; set; } = new();
    }

    public sealed class OrderCreationResult
    {
        public bool IsReplay { get; set; }
        public int PedidoId { get; set; }
        public decimal Total { get; set; }
        public OrderAmountSnapshot? Amounts { get; set; }
        public List<CartItemViewModel> Items { get; set; } = new();
        public List<CartItemViewModel> Gifts { get; set; } = new();
    }

    public sealed record OrderAmountSnapshot(decimal Gross, decimal Discount, decimal Net, decimal Tax, decimal FinalTotal);
}
