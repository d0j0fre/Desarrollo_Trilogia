using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-102 — Registrar órdenes de compra y confirmar su recepción.
    [AdminAuthorize("Compras", "PROVEEDORES_GESTIONAR")]
    public class PurchaseOrdersController : Controller
    {
        private readonly PurchasingDbService _purchasing;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<PurchaseOrdersController> _logger;

        public PurchaseOrdersController(PurchasingDbService purchasing, AdminDbService adminDbService, ILogger<PurchaseOrdersController> logger)
        {
            _purchasing = purchasing;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? estado, int? proveedorId)
        {
            ViewBag.Estado = estado;
            ViewBag.ProveedorId = proveedorId;
            ViewBag.Proveedores = await _purchasing.GetSuppliersAsync(true, null);
            return View(await _purchasing.GetPurchaseOrdersAsync(estado, proveedorId));
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new PurchaseOrderFormViewModel();
            await CargarListasAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrderFormViewModel model)
        {
            var seleccionados = model.Productos.Where(p => p.Seleccionado).ToList();
            if (seleccionados.Any(p => p.Cantidad < 1))
            {
                ModelState.AddModelError(string.Empty, "La cantidad de cada producto seleccionado debe ser mayor a cero.");
            }
            if (seleccionados.Any(p => p.PrecioUnitario <= 0))
            {
                ModelState.AddModelError(string.Empty, "El precio unitario de cada producto seleccionado debe ser mayor a cero.");
            }

            if (!ModelState.IsValid)
            {
                await CargarListasAsync(model);
                return View(model);
            }
            try
            {
                var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";
                var nuevaOrdenId = await _purchasing.CreatePurchaseOrderAsync(model, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Crear orden de compra", "Compras", $"Orden #{nuevaOrdenId} creada.");
                TempData["SuccessMessage"] = "Orden de compra creada correctamente.";
                return RedirectToAction(nameof(Detail), new { id = nuevaOrdenId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear orden de compra.");
                ModelState.AddModelError(string.Empty, "Ocurrió un error al crear la orden. Intente nuevamente.");
            }
            await CargarListasAsync(model);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Suggestions()
        {
            var sugerencias = await _purchasing.GetPurchaseSuggestionsAsync(mesesRecientes: 3, mesesCobertura: 1);

            // Completa el precio de referencia de tienda para cada producto sugerido.
            var productos = await _adminDbService.GetActiveProductsForSelectAsync();
            foreach (var s in sugerencias)
            {
                var producto = productos.FirstOrDefault(p => p.ProductoId == s.ProductoId);
                if (producto is not null) s.PrecioTienda = producto.Precio;
            }

            var model = new PurchaseOrderFormViewModel
            {
                Proveedores = await _purchasing.GetSuppliersAsync(true, null),
                Productos = sugerencias
            };

            // Reutiliza la vista y el formulario de Create — el POST sigue yendo a la acción Create.
            return View("Create", model);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var detalle = await _purchasing.GetPurchaseOrderDetailAsync(id);
            if (detalle is null) return NotFound();
            return View(detalle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int detalleOrdenCompraId, int ordenCompraId, int cantidadRecibidaAhora)
        {
            try
            {
                var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";
                await _purchasing.ReceivePurchaseOrderLineAsync(detalleOrdenCompraId, cantidadRecibidaAhora, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Recibir mercadería", "Compras",
                    $"Línea #{detalleOrdenCompraId} de la orden #{ordenCompraId}: +{cantidadRecibidaAhora} unidades.");
                TempData["SuccessMessage"] = "Recepción registrada e inventario actualizado.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar recepción de orden de compra.");
                TempData["ErrorMessage"] = ex.Message.StartsWith("La cantidad") || ex.Message.StartsWith("La línea")
                    ? ex.Message
                    : "No fue posible registrar la recepción.";
            }
            return RedirectToAction(nameof(Detail), new { id = ordenCompraId });
        }

        // Arma la lista de proveedores y productos disponibles, conservando lo ya marcado si el formulario recarga por error.
        private async Task CargarListasAsync(PurchaseOrderFormViewModel model)
        {
            model.Proveedores = await _purchasing.GetSuppliersAsync(true, null);
            var previo = model.Productos;
            var productos = await _adminDbService.GetActiveProductsForSelectAsync();
            var ultimosPrecios = await _purchasing.GetLastPurchasePricesAsync();
            model.Productos = productos.Select(p =>
            {
                var anterior = previo?.FirstOrDefault(x => x.ProductoId == p.ProductoId);
                return new PurchaseOrderLineSelectionViewModel
                {
                    ProductoId = p.ProductoId,
                    Nombre = p.Nombre,
                    StockActual = p.Stock,
                    PrecioTienda = p.Precio,
                    UltimoPrecioPagado = ultimosPrecios.TryGetValue(p.ProductoId, out var up) ? up : (decimal?)null,
                    Seleccionado = anterior?.Seleccionado ?? false,
                    Cantidad = anterior?.Cantidad ?? 1,
                    PrecioUnitario = anterior?.PrecioUnitario ?? 0
                };
            }).ToList();
        }

        private async Task RegistrarAuditoriaAsync(string accion, string modulo, string descripcion)
        {
            await _adminDbService.CreateAuditLogAsync(
                HttpContext.Session.GetInt32("UserId"),
                HttpContext.Session.GetString("UserFullName"),
                HttpContext.Session.GetString("UserEmail"),
                HttpContext.Session.GetString("UserRole"),
                accion, modulo, descripcion,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }
    }
}
