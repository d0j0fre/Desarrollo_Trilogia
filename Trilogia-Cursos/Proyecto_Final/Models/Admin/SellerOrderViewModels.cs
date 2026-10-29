using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Proyecto_Final.Models.Admin
{
    public class SellerOrderClientViewModel
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string DisplayText => $"{NombreCompleto} - {Correo}";
    }

    public class SellerOrderProductViewModel
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
    }

    public class SellerOrderProductInputViewModel
    {
        public int ProductoId { get; set; }
        [BindNever] public string Nombre { get; set; } = string.Empty;
        [BindNever] public string Categoria { get; set; } = string.Empty;
        [BindNever] public decimal Precio { get; set; }
        [BindNever] public int Stock { get; set; }

        [Range(0, 9999, ErrorMessage = "La cantidad no es válida.")]
        public int Cantidad { get; set; }

        public decimal Subtotal => Precio * Cantidad;
    }

    public class SellerOrderCreateViewModel
    {
        public Guid RequestId { get; set; }
        [StringLength(64, MinimumLength = 64)] public string? QuoteFingerprint { get; set; }
        [StringLength(64, MinimumLength = 64)] public string? QuoteSourceHash { get; set; }
        [Display(Name = "Cliente")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un cliente.")]
        public int ClienteUsuarioId { get; set; }

        [Display(Name = "Tipo de entrega")]
        [Required(ErrorMessage = "El tipo de entrega es obligatorio.")]
        [StringLength(50)]
        public string TipoEntrega { get; set; } = "Entrega por vendedor";

        [Display(Name = "Dirección de entrega")]
        [Required(ErrorMessage = "La dirección de entrega es obligatoria.")]
        [StringLength(255)]
        public string? DireccionEntrega { get; set; }

        [Display(Name = "Identificación del cliente")]
        [StringLength(100)]
        [CostaRicanIdentification]
        public string? IdentificacionCliente { get; set; }

        [Display(Name = "Observaciones")]
        [StringLength(255)]
        public string? Observaciones { get; set; }

        [BindNever] public List<SellerOrderClientViewModel> Clientes { get; set; } = new();
        public List<SellerOrderProductInputViewModel> Productos { get; set; } = new();

        public int TotalItems => Productos.Sum(x => x.Cantidad);
        public decimal Total => Productos.Sum(x => x.Subtotal);
    }

    public class SellerOfflineOrderProductViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "El producto no es válido.")]
        public int ProductoId { get; set; }

        [Range(1, 9999, ErrorMessage = "La cantidad no es válida.")]
        public int Cantidad { get; set; }
    }

    public class SellerOfflineOrderSyncRequestViewModel
    {
        [Required]
        public string PedidoOfflineGuid { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un cliente.")]
        public int ClienteUsuarioId { get; set; }

        [Required(ErrorMessage = "El tipo de entrega es obligatorio.")]
        [StringLength(50)]
        public string TipoEntrega { get; set; } = "Entrega por vendedor";

        [Required(ErrorMessage = "La dirección de entrega es obligatoria.")]
        [StringLength(255)]
        public string? DireccionEntrega { get; set; }

        [StringLength(100)]
        [CostaRicanIdentification]
        public string? IdentificacionCliente { get; set; }

        [StringLength(255)]
        public string? Observaciones { get; set; }

        public List<SellerOfflineOrderProductViewModel> Productos { get; set; } = new();
    }

    public class SellerOfflineOrderSyncResponseViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? PedidoId { get; set; }
        public string PedidoOfflineGuid { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
    }

    public class SellerOrderResultViewModel
    {
        public int PedidoId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int FacturaId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public bool EsRetenido => Estado == "Retenido";
        public bool TieneFactura => FacturaId > 0;
    }

    public class SellerMyOrderViewModel
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string MotivoRechazo { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
        public DateTime? FechaActualizacion { get; set; }
        public bool EsRetenido => Estado == "Retenido";
        public bool EsRechazado => Estado == "Rechazado";
        public bool EsLiberado => Estado == "Liberado";
        public bool TieneFactura => !string.IsNullOrWhiteSpace(NumeroFactura);
    }

    public sealed record SellerOrderReviewViewModel(SellerOrderCreateViewModel Draft,
        JJ.Commerce.ServerQuoteResult Quote, string CustomerName);
}
