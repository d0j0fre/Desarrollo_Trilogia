using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-131 — Tablero gerencial de indicadores. Accesible a Administrador y Gerente
    // mediante el permiso REPORTES_DASHBOARD. No modifica el dashboard admin existente.
    [AdminAuthorize("Reportes", "REPORTES_DASHBOARD")]
    public class ManagementDashboardController : Controller
    {
        // Indicadores que se pueden abrir para ver su gráfico detallado (CU-131-03).
        public static readonly IReadOnlyList<string> Indicators = new[] { "ventas", "stock", "ruta", "cobros" };

        private readonly ReportsDbService _reports;

        public ManagementDashboardController(ReportsDbService reports)
        {
            _reports = reports;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? rango, DateTime? desde, DateTime? hasta, string? indicador)
        {
            var (rangoDesde, rangoHasta, rangoNormalizado, aviso) = ResolveRange(rango, desde, hasta, DateTime.Today);

            var model = await _reports.GetDashboardAsync(rangoDesde, rangoHasta, rangoNormalizado);
            model.AvisoRango = aviso;

            var selected = (indicador ?? string.Empty).Trim().ToLowerInvariant();
            if (Indicators.Contains(selected))
            {
                model.IndicadorSeleccionado = selected;
                model.Detalle = await _reports.GetDashboardDetailAsync(selected, rangoDesde, rangoHasta);
            }

            return View(model);
        }

        /// <summary>
        /// Resuelve el rango pedido. Un rango personalizado con la fecha inicial mayor que la final
        /// se corrige intercambiando las fechas, pero ahora se avisa en pantalla (BUG-13-03).
        /// </summary>
        public static (DateTime Desde, DateTime Hasta, string Rango, string? Aviso) ResolveRange(
            string? rango, DateTime? desde, DateTime? hasta, DateTime hoy)
        {
            DateTime rangoDesde, rangoHasta;
            string rangoNormalizado;
            string? aviso = null;

            if (desde.HasValue && hasta.HasValue)
            {
                rangoDesde = desde.Value.Date;
                rangoHasta = hasta.Value.Date;
                rangoNormalizado = "personalizado";
            }
            else
            {
                rangoNormalizado = (rango ?? "hoy").ToLowerInvariant();
                switch (rangoNormalizado)
                {
                    case "semana":
                        rangoDesde = hoy.AddDays(-6);
                        rangoHasta = hoy;
                        break;
                    case "mes":
                        rangoDesde = new DateTime(hoy.Year, hoy.Month, 1);
                        rangoHasta = hoy;
                        break;
                    case "hoy":
                    default:
                        rangoNormalizado = "hoy";
                        rangoDesde = hoy;
                        rangoHasta = hoy;
                        break;
                }
            }

            if (rangoDesde > rangoHasta)
            {
                aviso = $"La fecha inicial ({rangoDesde:dd/MM/yyyy}) no puede ser posterior a la final ({rangoHasta:dd/MM/yyyy}). " +
                        $"Se invirtieron las fechas y se muestra el período {rangoHasta:dd/MM/yyyy} – {rangoDesde:dd/MM/yyyy}.";
                (rangoDesde, rangoHasta) = (rangoHasta, rangoDesde);
            }

            return (rangoDesde, rangoHasta, rangoNormalizado, aviso);
        }
    }
}
