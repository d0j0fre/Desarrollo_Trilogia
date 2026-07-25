using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-241/242/243 — Reportes de inteligencia de inventario. Mismo permiso que el tablero gerencial porque pertenecen al mismo módulo de Reportes.
    [AdminAuthorize("Reportes", "REPORTES_DASHBOARD")]
    public class InventoryIntelligenceController : Controller
    {
        private readonly ReportsDbService _reports;

        public InventoryIntelligenceController(ReportsDbService reports)
        {
            _reports = reports;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _reports.GetInventoryIntelligenceAsync();
            return View(model);
        }
    }
}
