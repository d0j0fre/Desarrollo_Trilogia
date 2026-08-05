using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-212 — Panel de progreso en tiempo real del vendedor sobre su propia meta.
    [SessionAuthorize("Vendedor")]
    public class MyGoalController : Controller
    {
        private readonly KpiDbService _kpis;

        public MyGoalController(KpiDbService kpis)
        {
            _kpis = kpis;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vendedorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var hoy = DateTime.Now;

            var model = await _kpis.GetMiProgresoAsync(vendedorId, hoy.Year, hoy.Month);
            return View(model);
        }
    }
}
