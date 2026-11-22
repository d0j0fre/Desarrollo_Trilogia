using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Finanzas", "FINANZAS_INGRESOS_EGRESOS_VER")]
    public class FinancialReportAdminController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public FinancialReportAdminController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? fechaInicio,
            DateTime? fechaFin)
        {
            // CU-094-04: un rango invertido no se consulta; se avisa en pantalla en vez de mostrar ceros.
            if (RangoInvalido(fechaInicio, fechaFin))
            {
                ViewBag.ErrorRango = "La fecha inicial no puede ser posterior a la fecha final. Corrija el rango y vuelva a consultar.";
                return View(new FinancialReportViewModel { FechaInicio = fechaInicio, FechaFin = fechaFin });
            }

            var modelo = await _adminDbService
                .GetFinancialReportAsync(fechaInicio, fechaFin);

            return View(modelo);
        }

        public static bool RangoInvalido(DateTime? fechaInicio, DateTime? fechaFin) =>
            fechaInicio.HasValue && fechaFin.HasValue && fechaInicio.Value.Date > fechaFin.Value.Date;
    }
}
