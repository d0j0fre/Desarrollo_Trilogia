using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-181 — Combos: agrupar productos para venderlos como paquete promocional.
    [AdminAuthorize("Inventario")]
    public class CombosController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public CombosController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var combos = await _adminDbService.GetCombosAsync();
            return View(combos);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var combo = await _adminDbService.GetComboDetailAsync(id);
            if (combo is null) return NotFound();
            return View(combo);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ComboFormViewModel
            {
                Productos = await GetProductSelectionListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComboFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Productos = await GetProductSelectionListAsync(model.Productos);
                return View(model);
            }

            try
            {
                var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";
                var nuevoComboId = await _adminDbService.CreateComboAsync(model, usuarioId, usuarioNombre);
                TempData["SuccessMessage"] = "Combo creado correctamente.";
                return RedirectToAction(nameof(Detail), new { id = nuevoComboId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error al crear el combo. Intente nuevamente.");
            }

            model.Productos = await GetProductSelectionListAsync(model.Productos);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            await _adminDbService.ToggleComboStatusAsync(id);
            TempData["SuccessMessage"] = "Estado del combo actualizado.";
            return RedirectToAction(nameof(Index));
        }

        // Arma la lista de productos disponibles, conservando lo ya marcado si el formulario se recarga por un error.
        private async Task<List<ComboProductSelectionViewModel>> GetProductSelectionListAsync(List<ComboProductSelectionViewModel>? previo = null)
        {
            var productos = await _adminDbService.GetActiveProductsForSelectAsync();
            return productos.Select(p =>
            {
                var anterior = previo?.FirstOrDefault(x => x.ProductoId == p.ProductoId);
                return new ComboProductSelectionViewModel
                {
                    ProductoId = p.ProductoId,
                    Nombre = p.Nombre,
                    StockActual = p.Stock,
                    Seleccionado = anterior?.Seleccionado ?? false,
                    Cantidad = anterior?.Cantidad ?? 1
                };
            }).ToList();
        }
    }
}
