namespace Proyecto_Final.Models.Admin
{
    public class ManagementDashboardViewModel
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        // "hoy" | "semana" | "mes" | "personalizado"
        public string Rango { get; set; } = "hoy";
        // Texto cuando las fechas del rango personalizado venían invertidas y se corrigieron.
        public string? AvisoRango { get; set; }

        public decimal VentasPeriodo { get; set; }
        public int FacturasPeriodo { get; set; }
        public int PedidosPeriodo { get; set; }
        public decimal TicketPromedio { get; set; }
        public int StockBajo { get; set; }
        public int ProductosAgotados { get; set; }
        public int PedidosEnRuta { get; set; }
        public decimal CobrosPendientes { get; set; }
        public bool HayDatos { get; set; }

        public List<SalesSeriePointViewModel> Serie { get; set; } = new();

        // Indicador abierto ("ventas" | "stock" | "ruta" | "cobros") y sus gráficos detallados (CU-131-03).
        public string? IndicadorSeleccionado { get; set; }
        public List<DashboardDetailGroupViewModel> Detalle { get; set; } = new();
    }

    public class SalesSeriePointViewModel
    {
        public DateTime Dia { get; set; }
        public decimal Total { get; set; }
        public int Facturas { get; set; }
    }

    // Un gráfico de barras del detalle de un indicador (por ejemplo «Ventas por categoría»).
    public class DashboardDetailGroupViewModel
    {
        public string Clave { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public bool EsMoneda { get; set; }
        public List<DashboardDetailItemViewModel> Items { get; set; } = new();
    }

    public class DashboardDetailItemViewModel
    {
        public string Etiqueta { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public string? Detalle { get; set; }
    }
}
