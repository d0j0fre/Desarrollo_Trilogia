using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    public class RetainedOrderListItemViewModel
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public string VendedorNombre { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string TipoEntrega { get; set; } = string.Empty;
        public string CanalPedido { get; set; } = string.Empty;
        public int TotalLineas { get; set; }
    }

    public class ManagerApproveResultViewModel
    {
        public int PedidoId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int FacturaId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
    }

    public class ManagerRejectOrderViewModel
    {
        public Guid OperationId { get; set; }
        [Range(1, long.MaxValue)] public long ExpectedRevision { get; set; }
        [Required]
        public int PedidoId { get; set; }

        [Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
        [StringLength(500, ErrorMessage = "El motivo no puede superar 500 caracteres.")]
        [Display(Name = "Motivo de rechazo")]
        public string MotivoRechazo { get; set; } = string.Empty;
    }

    public class ManagerOrderReviewViewModel
    {
        public Guid OperationId { get; set; }
        public long ExpectedRevision { get; set; }
        public OrderDetailViewModel Pedido { get; set; } = new();
        public ManagerRejectOrderViewModel RechazarForm { get; set; } = new();
    }
}
