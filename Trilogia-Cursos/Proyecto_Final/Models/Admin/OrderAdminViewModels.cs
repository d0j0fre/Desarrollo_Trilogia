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
        public string MetodoPago { get; set; } = string.Empty;
        public string EstadoPago { get; set; } = string.Empty;
        public string? ReferenciaPago { get; set; }
        public DateTime? FechaPago { get; set; }
        public string? MotivoRechazo { get; set; }
        public bool HasInvoice { get; set; }
        public int? FacturaId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public bool CanGenerateInvoice => !HasInvoice
            && Detalles.Any()
            && (Estado == "Pendiente" || Estado == "Aprobado" || Estado == "EnProceso"
                || Estado == "Entregado" || Estado == "Liberado");
        public List<OrderDetailLineViewModel> Detalles { get; set; } = new();
        public List<string> EstadosDisponibles { get; set; } = new()
        {
            "Pendiente", "Aprobado", "EnProceso", "Entregado", "Cancelado",
            "Retenido", "Liberado", "Rechazado"
        };
    }


    public class GenerateInvoiceResultViewModel
    {
        public int FacturaId { get; set; }
        public int PedidoId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public string EstadoPedido { get; set; } = string.Empty;
    }
}
