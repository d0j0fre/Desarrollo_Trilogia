namespace Proyecto_Final.Models.Admin
{
    // CU-173 — Promoción vigente (fila de sp_Promociones_Vigentes) para el motor del carrito.
    public class ActivePromotionViewModel
    {
        public int PromocionId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public int ProductoId { get; set; }
        public int CantidadMinima { get; set; }
        public decimal? PorcentajeDescuento { get; set; }
        public int? ProductoRegaloId { get; set; }
        public string? ProductoRegaloNombre { get; set; }
        public decimal ProductoRegaloPrecio { get; set; }
        public int ProductoRegaloStock { get; set; }
        public int? CantidadRegalo { get; set; }
        public int Prioridad { get; set; }
        public string SegmentoCliente { get; set; } = "Todos";
        public DateTime FechaInicio { get; set; } = DateTime.MinValue;
        public DateTime FechaFin { get; set; } = DateTime.MaxValue;
    }

}
