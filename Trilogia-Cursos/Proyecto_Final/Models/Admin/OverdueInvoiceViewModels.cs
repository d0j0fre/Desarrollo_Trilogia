namespace Proyecto_Final.Models.Admin
{
    /// <summary>
    /// POST-qa2-03 · CU-093-04: factura a crédito vencida (cargo con saldo y fecha de vencimiento pasada, 0326).
    /// Es un cálculo: dbo.Facturas.Estado no cambia.
    /// </summary>
    public sealed class OverdueInvoiceViewModel
    {
        public int FacturaId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public int PedidoId { get; set; }
        public int UsuarioId { get; set; }
        public decimal MontoCargo { get; set; }
        public decimal Pendiente { get; set; }
        public DateTime Vencimiento { get; set; }
        public int DiasAtraso { get; set; }

        public string Descripcion => $"Vencida hace {DiasAtraso} día(s) (venció el {Vencimiento:dd/MM/yyyy}); pendiente ₡{Pendiente:N2}";
    }
}
