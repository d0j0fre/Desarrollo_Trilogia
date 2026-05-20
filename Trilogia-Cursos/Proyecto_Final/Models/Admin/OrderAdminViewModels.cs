namespace Proyecto_Final.Models.Admin
{
    public class OrderAdminListItemViewModel
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? TipoEntrega { get; set; }
        public string? DireccionEntrega { get; set; }
        public decimal Total { get; set; }
        public int TotalLineas { get; set; }
    }

    public class OrderDetailViewModel
    {
        public int PedidoId { get; set; }
        public int UsuarioId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? TipoEntrega { get; set; }
        public string? DireccionEntrega { get; set; }
        public decimal Total { get; set; }
        public string? Observaciones { get; set; }
        public List<OrderDetailLineViewModel> Detalles { get; set; } = new();
        public List<string> EstadosDisponibles { get; set; } = new() { "Pendiente", "Aprobado", "EnProceso", "Entregado", "Cancelado" };
    }

    public class OrderDetailLineViewModel
    {
        public int PedidoDetalleId { get; set; }
        public string Producto { get; set; } = string.Empty;
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public int StockActual { get; set; }
    }
}
