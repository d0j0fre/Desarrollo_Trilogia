using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[AdminAuthorize("Inventario", "INVENTARIO_INTELIGENCIA_VER")]
public sealed class InventoryIntelligenceController : Controller
{
    private readonly IInventoryIntelligenceService _intelligence;
    private readonly ILogger<InventoryIntelligenceController> _logger;

    public InventoryIntelligenceController(
        IInventoryIntelligenceService intelligence,
        ILogger<InventoryIntelligenceController> logger)
    {
        _intelligence = intelligence;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] InventoryIntelligenceFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // BUG-24-06: parámetros no válidos (por ejemplo, año inicial mayor que el final): se muestran
            // los mensajes de validación y no se genera ningún análisis.
            return View(new InventoryIntelligenceViewModel
            {
                Filter = filter,
                Categorias = await TryGetCategoriesAsync(cancellationToken)
            });
        }

        try
        {
            return View(await _intelligence.GetAsync(filter, cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No fue posible consultar inteligencia de inventario.");
            ModelState.AddModelError(string.Empty, "No fue posible cargar los indicadores en este momento.");
            return View(new InventoryIntelligenceViewModel { Filter = filter });
        }
    }

    private async Task<List<string>> TryGetCategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _intelligence.GetCategoriesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No fue posible cargar las categorías de la tendencia estacional.");
            return new List<string>();
        }
    }
}
