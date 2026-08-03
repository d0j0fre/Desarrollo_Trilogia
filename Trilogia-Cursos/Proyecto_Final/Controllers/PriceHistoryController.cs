using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-104 — Comparar precios históricos de un producto entre proveedores.
    [AdminAuthorize("Compras", "PROVEEDORES_GESTIONAR")]
    public class PriceHistoryController : Controller
    {
        private readonly PurchasingDbService _purchasing;
        private readonly AdminDbService _adminDbService;

        public PriceHistoryController(PurchasingDbService purchasing, AdminDbService adminDbService)
        {
            _purchasing = purchasing;
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? productoId)
        {
            var model = new PriceHistoryViewModel
            {
                ProductosDisponibles = await _adminDbService.GetActiveProductsForSelectAsync()
            };

            if (productoId.HasValue)
            {
                model.ProductoId = productoId.Value;
                model.ProductoNombre = model.ProductosDisponibles.FirstOrDefault(p => p.ProductoId == productoId.Value)?.Nombre ?? string.Empty;
                model.Historial = await _purchasing.GetPriceHistoryAsync(productoId.Value);
            }

            return View(model);
        }
    }
}
