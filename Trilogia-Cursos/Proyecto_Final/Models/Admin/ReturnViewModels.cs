using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    // CU-141 / CU-142 — Devoluciones y cuarentena de productos.

    public class ReturnListItemViewModel
    {
        public int DevolucionId { get; set; }
        public int? PedidoId { get; set; }
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string ClienteInfo { get; set; } = string.Empty;
        public string RegistradoPorNombre { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        public string ResueltoPorNombre { get; set; } = string.Empty;
        public DateTime? FechaResolucion { get; set; }
        public string ObservacionResolucion { get; set; } = string.Empty;
    }

    public class ReturnFormViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un producto.")]
        public int ProductoId { get; set; }

        public int? PedidoId { get; set; }

        [Range(1, 1000000, ErrorMessage = "La cantidad debe ser mayor a cero.")]
        public int Cantidad { get; set; } = 1;

        [Required(ErrorMessage = "El motivo es obligatorio.")]
        [StringLength(300, ErrorMessage = "El motivo no puede exceder 300 caracteres.")]
        public string Motivo { get; set; } = string.Empty;

        [StringLength(150, ErrorMessage = "El dato del cliente no puede exceder 150 caracteres.")]
        public string? ClienteInfo { get; set; }
    }

    public class ReturnsIndexViewModel
    {
        public List<ReturnListItemViewModel> Devoluciones { get; set; } = new();
        public ReturnFormViewModel Nueva { get; set; } = new();
        public string? Buscar { get; set; }
        public string? Estado { get; set; }
    }

    // CU-143-04 — Reporte de devoluciones por producto, cliente y motivo.
    public class ReturnsReportGroupViewModel
    {
        public string Clave { get; set; } = string.Empty;
        public int Devoluciones { get; set; }
        public int Unidades { get; set; }
    }

    public class ReturnsReportViewModel
    {
        public const string SinCliente = "Sin cliente / referencia";

        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public List<ReturnListItemViewModel> Devoluciones { get; set; } = new();
        public List<ReturnsReportGroupViewModel> PorProducto { get; set; } = new();
        public List<ReturnsReportGroupViewModel> PorCliente { get; set; } = new();
        public List<ReturnsReportGroupViewModel> PorMotivo { get; set; } = new();
        public int TotalUnidades => Devoluciones.Sum(d => d.Cantidad);

        /// <summary>Filtra por fecha de registro (ambos días incluidos) y agrupa.</summary>
        public static ReturnsReportViewModel Build(IEnumerable<ReturnListItemViewModel> devoluciones, DateTime desde, DateTime hasta)
        {
            var inicio = desde.Date;
            var fin = hasta.Date.AddDays(1);
            var enRango = devoluciones
                .Where(d => d.FechaRegistro >= inicio && d.FechaRegistro < fin)
                .OrderByDescending(d => d.FechaRegistro)
                .ToList();

            return new ReturnsReportViewModel
            {
                Desde = inicio,
                Hasta = hasta.Date,
                Devoluciones = enRango,
                PorProducto = Agrupar(enRango, d => d.ProductoNombre),
                PorCliente = Agrupar(enRango, d => string.IsNullOrWhiteSpace(d.ClienteInfo) ? SinCliente : d.ClienteInfo),
                PorMotivo = Agrupar(enRango, d => d.Motivo)
            };
        }

        private static List<ReturnsReportGroupViewModel> Agrupar(
            IEnumerable<ReturnListItemViewModel> devoluciones,
            Func<ReturnListItemViewModel, string> clave)
        {
            return devoluciones
                .GroupBy(d => clave(d).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new ReturnsReportGroupViewModel
                {
                    Clave = g.Key,
                    Devoluciones = g.Count(),
                    Unidades = g.Sum(d => d.Cantidad)
                })
                .OrderByDescending(g => g.Unidades)
                .ThenBy(g => g.Clave, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
