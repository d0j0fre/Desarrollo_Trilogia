namespace Proyecto_Final.Models.Store;

/// <summary>G3-15 (0256): datos que el cliente indica al pagar en la Tienda y que se usan para facturar.</summary>
public sealed class OrderCustomerDataViewModel
{
    public int PedidoId { get; set; }
    public string TipoCliente { get; set; } = string.Empty;
    public bool FacturaElectronica { get; set; }
    public string? CorreoComprobante { get; set; }
    public string? Telefono { get; set; }
    public string? TelefonoAlterno { get; set; }
}
