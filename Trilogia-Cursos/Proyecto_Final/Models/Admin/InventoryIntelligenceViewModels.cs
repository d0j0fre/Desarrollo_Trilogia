namespace Proyecto_Final.Models.Admin
{
    // CU-241/242/243 — Contenedor de los 3 reportes de inteligencia de inventario.
    public class InventoryIntelligenceViewModel
    {
        public List<PurchaseSuggestionItem> SugerenciasCompra { get; set; } = new();
        public List<SlowMovingProductItem> ProductosEstancados { get; set; } = new();
        public List<SeasonalTrendPoint> TendenciaEstacional { get; set; } = new();
    }

    // CU-241 — Fila de sugerencia de compra por producto.
    public class PurchaseSuggestionItem
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public decimal PromedioVentaMensual { get; set; }
        public int CantidadSugerida { get; set; }
    }

    // CU-242 — Fila de producto con riesgo de estancamiento.
    public class SlowMovingProductItem
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int VendidoUltimosMeses { get; set; }
    }

    // CU-243 — Punto del gráfico de tendencia estacional (por mes del año).
    public class SeasonalTrendPoint
    {
        public int NumeroMes { get; set; }
        public string NombreMes { get; set; } = string.Empty;
        public decimal TotalVendido { get; set; }
    }
}
