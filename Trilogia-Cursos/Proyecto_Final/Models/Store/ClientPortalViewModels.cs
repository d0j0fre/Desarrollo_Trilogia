namespace Proyecto_Final.Models.Store
{
    public class ClientPortalIndexViewModel
    {
        public ClientPortalSummaryViewModel Cliente { get; set; } = new();
        public ClientPortalCreditSummaryViewModel? Credito { get; set; }
        public List<ClientPortalOrderListItemViewModel> Pedidos { get; set; } = new();
    }

    public class ClientPortalSummaryViewModel
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaRegistro { get; set; }
        public int TotalPedidos { get; set; }
        public decimal TotalComprado { get; set; }
        public DateTime? UltimoPedido { get; set; }
    }

    public class ClientPortalCreditSummaryViewModel
    {
        public decimal LimiteCredito { get; set; }
        public bool CreditoActivo { get; set; }
        public bool CreditoBloqueado { get; set; }
        public decimal DeudaActual { get; set; }
        public decimal CreditoDisponible { get; set; }
        public DateTime? FechaActualizacion { get; set; }
    }

    public class ClientPortalOrderListItemViewModel
    {
        public int PedidoId { get; set; }
        public DateTime FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? TipoEntrega { get; set; }
        public string? DireccionEntrega { get; set; }
        public decimal Total { get; set; }
        public string? Observaciones { get; set; }
    }

    public class ClientPortalOrderDetailViewModel
    {
        public int PedidoId { get; set; }
        public DateTime FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? TipoEntrega { get; set; }
        public string? DireccionEntrega { get; set; }
        public decimal Total { get; set; }
        public string? Observaciones { get; set; }
        public bool HasInvoice { get; set; }
        public bool CanCancel { get; set; }
        public string CancelStatusMessage { get; set; } = string.Empty;
        public List<ClientPortalOrderLineViewModel> Lineas { get; set; } = new();
    }

    public class ClientPortalOrderLineViewModel
    {
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}
