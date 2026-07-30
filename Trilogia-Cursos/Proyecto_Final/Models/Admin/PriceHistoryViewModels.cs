namespace Proyecto_Final.Models.Admin
{
    // CU-104 — Histórico de precios de compra por producto y proveedor.
    public class PriceHistoryLineViewModel
    {
        public int OrdenCompraId { get; set; }
        public int ProveedorId { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
    }

    public class PriceHistoryViewModel
    {
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public List<PriceHistoryLineViewModel> Historial { get; set; } = new();
        public List<ProductAdminViewModel> ProductosDisponibles { get; set; } = new();
    }
}
