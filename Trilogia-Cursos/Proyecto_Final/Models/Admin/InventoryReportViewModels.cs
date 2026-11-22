namespace Proyecto_Final.Models.Admin
{
    public sealed class InventoryReportRowViewModel
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int StockMinimo { get; set; }
        public decimal Precio { get; set; }
        public decimal ValorInventario => Stock * Precio;
        public string Estado { get; set; } = InventoryReportViewModel.OkStatus;
        public bool Activo { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        public static InventoryReportRowViewModel From(ProductAdminViewModel p) => new()
        {
            ProductoId = p.ProductoId,
            Nombre = p.Nombre,
            Categoria = p.Categoria,
            Stock = p.Stock,
            StockMinimo = p.StockMinimo,
            Precio = p.Precio,
            Activo = p.Activo,
            FechaVencimiento = p.FechaVencimiento,
            Estado = Classify(p)
        };

        // Misma regla que la pantalla Inventario: agotado, bajo (al mínimo o por debajo) o normal.
        public static string Classify(ProductAdminViewModel p)
        {
            // POST-qa2-06: el estado depende del stock mínimo de cada producto; EstadoStock de la base usa un tope fijo de 5.
            if (p.Stock <= 0)
                return InventoryReportViewModel.OutStatus;
            if (p.Stock <= p.StockMinimo)
                return InventoryReportViewModel.LowStatus;
            return InventoryReportViewModel.OkStatus;
        }
    }

    public sealed class InventoryReportViewModel
    {
        public const string AllStatuses = "todos";
        public const string OutStatus = "Agotado";
        public const string LowStatus = "Bajo";
        public const string OkStatus = "Normal";

        public string? Filtro { get; set; }
        public string Estado { get; set; } = AllStatuses;
        public bool IncluirInactivos { get; set; }
        public List<InventoryReportRowViewModel> Filas { get; set; } = new();

        public int TotalProductos => Filas.Count;
        public int TotalUnidades => Filas.Sum(f => f.Stock);
        public decimal ValorInventario => Filas.Sum(f => f.ValorInventario);
        public int Agotados => Filas.Count(f => f.Estado == OutStatus);
        public int StockBajo => Filas.Count(f => f.Estado == LowStatus);

        public static string NormalizeStatus(string? estado) => estado?.Trim().ToLowerInvariant() switch
        {
            "agotado" => OutStatus,
            "bajo" => LowStatus,
            "normal" => OkStatus,
            _ => AllStatuses
        };
    }
}
