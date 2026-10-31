namespace Proyecto_Final.Models.Admin
{
    // BUG-06-03 / BUG-12-01 / BUG-12-02 — estado de cuenta único del cliente (sp_Cliente_EstadoCuenta, 0232).
    // Lo usan el detalle del cliente, el portal y los PDF, así que el saldo es el mismo en todos lados.
    public class ClientAccountStatementViewModel
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public decimal LimiteCredito { get; set; }
        public bool CreditoActivo { get; set; }
        public bool CreditoBloqueado { get; set; }
        public int PlazoCreditoDias { get; set; }
        public decimal Saldo { get; set; }
        public decimal DeudaVencida { get; set; }
        public DateTime? VencimientoMasAntiguo { get; set; }
        public decimal CreditoDisponible { get; set; }
        public int CantidadFacturas { get; set; }
        public decimal TotalFacturado { get; set; }
        public decimal TotalPagado { get; set; }
        public DateTime FechaCorte { get; set; }
        public List<ClientAccountInvoiceViewModel> Facturas { get; set; } = new();
        public List<ClientAccountMovementViewModel> Movimientos { get; set; } = new();
    }

    public class ClientAccountInvoiceViewModel
    {
        public int FacturaId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public int PedidoId { get; set; }
        public DateTime FechaFactura { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Impuesto { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? MetodoPago { get; set; }
    }

    public class ClientAccountMovementViewModel
    {
        public int CreditoMovimientoId { get; set; }
        public DateTime FechaMovimiento { get; set; }
        public string TipoMovimiento { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string? Referencia { get; set; }
        public int? PedidoId { get; set; }
        public decimal Cargo { get; set; }
        public decimal Abono { get; set; }
        public decimal SaldoAcumulado { get; set; }

        public string TipoTexto => TipoMovimiento switch
        {
            "Cargo" => "Cargo",
            "Abono" => "Pago",
            "AjustePositivo" => "Ajuste +",
            "AjusteNegativo" => "Ajuste -",
            _ => TipoMovimiento
        };
    }
}
