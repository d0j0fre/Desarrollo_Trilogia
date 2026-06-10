using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    public class SellerOrdersController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public SellerOrdersController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAuthorizedSeller())
                return RedirectToAction("Login", "Account");

            var model = await BuildCreateModelAsync(new SellerOrderCreateViewModel());
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SellerOrderCreateViewModel model)
        {
            if (!IsAuthorizedSeller())
                return RedirectToAction("Login", "Account");

            var productosSeleccionados = model.Productos
                .Where(x => x.Cantidad > 0)
                .ToList();

            if (!productosSeleccionados.Any())
            {
                ModelState.AddModelError(string.Empty, "Debe seleccionar al menos un producto para registrar el pedido.");
            }

            if (!ModelState.IsValid)
            {
                model.Productos = productosSeleccionados.Any() ? MergeSelectedProducts(await _adminDbService.GetSellerOrderProductsAsync(), productosSeleccionados) : new List<SellerOrderProductInputViewModel>();
                return View("Index", await BuildCreateModelAsync(model));
            }

            model.Productos = productosSeleccionados;

            var vendedorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var vendedorNombre = HttpContext.Session.GetString("UserFullName") ?? "Vendedor";

            try
            {
                var pedidoId = await _adminDbService.CreateSellerOrderAsync(model, vendedorId, vendedorNombre);

                await RegistrarAuditoriaAsync(
                    "Registrar pedido móvil",
                    "Venta móvil",
                    $"El usuario {vendedorNombre} registró el pedido móvil #{pedidoId} para el cliente #{model.ClienteUsuarioId}.");

                TempData["SuccessMessage"] = $"Pedido móvil #{pedidoId} registrado correctamente.";
                return RedirectToAction("Detail", "OrdersAdmin", new { id = pedidoId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Index", await BuildCreateModelAsync(model));
            }
        }

        private async Task<SellerOrderCreateViewModel> BuildCreateModelAsync(SellerOrderCreateViewModel model)
        {
            model.Clientes = await _adminDbService.GetSellerOrderClientsAsync();

            var productosDisponibles = await _adminDbService.GetSellerOrderProductsAsync();
            model.Productos = MergeSelectedProducts(productosDisponibles, model.Productos);

            return model;
        }

        private static List<SellerOrderProductInputViewModel> MergeSelectedProducts(
            List<SellerOrderProductViewModel> productosDisponibles,
            List<SellerOrderProductInputViewModel>? productosSeleccionados)
        {
            var cantidades = productosSeleccionados?
                .GroupBy(x => x.ProductoId)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Cantidad))
                ?? new Dictionary<int, int>();

            return productosDisponibles.Select(producto => new SellerOrderProductInputViewModel
            {
                ProductoId = producto.ProductoId,
                Nombre = producto.Nombre,
                Categoria = producto.Categoria,
                Precio = producto.Precio,
                Stock = producto.Stock,
                Cantidad = cantidades.TryGetValue(producto.ProductoId, out var cantidad) ? Math.Max(cantidad, 0) : 0
            }).ToList();
        }

        private bool IsAuthorizedSeller()
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrWhiteSpace(email))
                return false;

            return string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Empleado", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Vendedor", StringComparison.OrdinalIgnoreCase);
        }

        private async Task RegistrarAuditoriaAsync(string accion, string modulo, string descripcion)
        {
            await _adminDbService.CreateAuditLogAsync(
                HttpContext.Session.GetInt32("UserId"),
                HttpContext.Session.GetString("UserFullName"),
                HttpContext.Session.GetString("UserEmail"),
                HttpContext.Session.GetString("UserRole"),
                accion,
                modulo,
                descripcion,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }
    }
}
