using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-133 — Reporte de inventario con exportación a Excel (.xlsx, generado en el navegador).
    // El reporte muestra el valor del inventario: solo Administrador y Gerente, además del permiso de ver inventario.
    [SessionAuthorize("Administrador", "Gerente")]
    [AdminAuthorize("Inventario", "INVENTARIO_VER")]
    public class InventoryReportController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public InventoryReportController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(string? filtro, string? estado, bool incluirInactivos = false)
        {
            var productos = await _adminDbService.GetProductsAsync(filtro);
            return View(Build(productos, filtro, estado, incluirInactivos));
        }

        public static InventoryReportViewModel Build(
            IEnumerable<ProductAdminViewModel> productos, string? filtro, string? estado, bool incluirInactivos)
        {
            var estadoNormalizado = InventoryReportViewModel.NormalizeStatus(estado);
            var filas = productos
                .Where(p => incluirInactivos || p.Activo)
                .Select(InventoryReportRowViewModel.From)
                .Where(r => estadoNormalizado == InventoryReportViewModel.AllStatuses || r.Estado == estadoNormalizado)
                .OrderBy(r => r.Categoria)
                .ThenBy(r => r.Nombre)
                .ToList();

            return new InventoryReportViewModel
            {
                Filtro = filtro?.Trim(),
                Estado = estadoNormalizado,
                IncluirInactivos = incluirInactivos,
                Filas = filas
            };
        }
    }
}
