using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [SessionAuthorize("Administrador", "Empleado", "Vendedor")]
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
                var result = await _adminDbService.CreateSellerOrderAsync(model, vendedorId, vendedorNombre);

                await RegistrarAuditoriaAsync(
                    result.EsRetenido ? "Registrar pedido retenido" : "Registrar pedido móvil",
                    "Venta móvil",
                    $"El usuario {vendedorNombre} registró el pedido #{result.PedidoId} para el cliente #{model.ClienteUsuarioId}. Estado: {result.Estado}.");

                if (result.EsRetenido)
                    return RedirectToAction(nameof(RetainedConfirmation), new { id = result.PedidoId });

                TempData["ConfirmNumeroFactura"] = result.NumeroFactura;
                return RedirectToAction(nameof(Confirmation), new { id = result.PedidoId });
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "No fue posible registrar el pedido en este momento. Intente nuevamente.");
                return View("Index", await BuildCreateModelAsync(model));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncOffline([FromBody] SellerOfflineOrderSyncRequestViewModel request)
        {
            if (!IsAuthorizedSeller())
            {
                return Unauthorized(new SellerOfflineOrderSyncResponseViewModel
                {
                    Success = false,
                    Message = "La sesión no está activa. Inicie sesión nuevamente para sincronizar pedidos offline.",
                    PedidoOfflineGuid = request?.PedidoOfflineGuid ?? string.Empty
                });
            }

            if (request == null)
            {
                return Json(new SellerOfflineOrderSyncResponseViewModel
                {
                    Success = false,
                    Message = "No se recibió información del pedido offline."
                });
            }

            if (!Guid.TryParse(request.PedidoOfflineGuid, out var pedidoOfflineGuid))
            {
                return Json(new SellerOfflineOrderSyncResponseViewModel
                {
                    Success = false,
                    Message = "El identificador offline del pedido no es válido.",
                    PedidoOfflineGuid = request.PedidoOfflineGuid
                });
            }

            var productosSeleccionados = request.Productos
                .Where(x => x.Cantidad > 0)
                .GroupBy(x => x.ProductoId)
                .Select(x => new SellerOrderProductInputViewModel
                {
                    ProductoId = x.Key,
                    Cantidad = x.Sum(y => y.Cantidad)
                })
                .ToList();

            if (request.ClienteUsuarioId <= 0)
                ModelState.AddModelError(nameof(request.ClienteUsuarioId), "Debe seleccionar un cliente.");

            if (string.IsNullOrWhiteSpace(request.TipoEntrega))
                ModelState.AddModelError(nameof(request.TipoEntrega), "El tipo de entrega es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.DireccionEntrega))
                ModelState.AddModelError(nameof(request.DireccionEntrega), "La dirección de entrega es obligatoria.");

            if (!productosSeleccionados.Any())
                ModelState.AddModelError(nameof(request.Productos), "Debe seleccionar al menos un producto.");

            if (!ModelState.IsValid)
            {
                var error = ModelState.Values
                    .SelectMany(x => x.Errors)
                    .Select(x => x.ErrorMessage)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "El pedido offline tiene datos incompletos.";

                return Json(new SellerOfflineOrderSyncResponseViewModel
                {
                    Success = false,
                    Message = error,
                    PedidoOfflineGuid = request.PedidoOfflineGuid
                });
            }

            var vendedorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var vendedorNombre = HttpContext.Session.GetString("UserFullName") ?? "Vendedor";

            var model = new SellerOrderCreateViewModel
            {
                ClienteUsuarioId = request.ClienteUsuarioId,
                TipoEntrega = request.TipoEntrega.Trim(),
                DireccionEntrega = request.DireccionEntrega?.Trim(),
                IdentificacionCliente = string.IsNullOrWhiteSpace(request.IdentificacionCliente) ? null : request.IdentificacionCliente.Trim(),
                Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
                Productos = productosSeleccionados
            };

            try
            {
                var result = await _adminDbService.CreateSellerOrderAsync(
                    model,
                    vendedorId,
                    vendedorNombre,
                    pedidoOfflineGuid,
                    "Venta móvil offline");

                await RegistrarAuditoriaAsync(
                    result.EsRetenido ? "Sincronizar pedido offline retenido" : "Sincronizar pedido offline",
                    "Venta móvil",
                    $"El usuario {vendedorNombre} sincronizó el pedido offline {pedidoOfflineGuid} como pedido #{result.PedidoId}. Estado: {result.Estado}.");

                var redirectUrl = result.EsRetenido
                    ? Url.Action(nameof(RetainedConfirmation), "SellerOrders", new { id = result.PedidoId }) ?? string.Empty
                    : Url.Action(nameof(Confirmation),         "SellerOrders", new { id = result.PedidoId }) ?? string.Empty;

                return Json(new SellerOfflineOrderSyncResponseViewModel
                {
                    Success       = true,
                    Message       = result.EsRetenido
                        ? $"Pedido #{result.PedidoId} enviado a revisión del Gerente por superar el umbral de autorización."
                        : $"Pedido #{result.PedidoId} sincronizado y facturado como {result.NumeroFactura}.",
                    PedidoId      = result.PedidoId,
                    PedidoOfflineGuid = request.PedidoOfflineGuid,
                    Estado        = result.Estado,
                    NumeroFactura = result.NumeroFactura,
                    RedirectUrl   = redirectUrl
                });
            }
            catch (Exception)
            {
                return Json(new SellerOfflineOrderSyncResponseViewModel
                {
                    Success = false,
                    Message = "No fue posible sincronizar el pedido offline en este momento. Intente nuevamente.",
                    PedidoOfflineGuid = request.PedidoOfflineGuid
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            if (!IsAuthorizedSeller())
                return RedirectToAction("Login", "Account");

            var pedido = await _adminDbService.GetOrderDetailAsync(id);
            if (pedido == null)
                return RedirectToAction(nameof(Index));

            var factura = await _adminDbService.GetInvoiceSummaryByOrderAsync(id);
            if (factura.HasValue)
            {
                pedido.FacturaId      = factura.Value.FacturaId;
                pedido.NumeroFactura  = factura.Value.NumeroFactura;
                pedido.HasInvoice     = true;
            }

            ViewBag.NumeroFactura = TempData["ConfirmNumeroFactura"] as string ?? pedido.NumeroFactura;
            return View(pedido);
        }

        [HttpGet]
        public async Task<IActionResult> RetainedConfirmation(int id)
        {
            if (!IsAuthorizedSeller())
                return RedirectToAction("Login", "Account");

            var pedido = await _adminDbService.GetOrderDetailAsync(id);
            if (pedido == null)
                return RedirectToAction(nameof(Index));

            return View(pedido);
        }

        [HttpGet]
        public async Task<IActionResult> MyOrders()
        {
            if (!IsAuthorizedSeller())
                return RedirectToAction("Login", "Account");

            var vendedorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var pedidos = await _adminDbService.GetSellerMyOrdersAsync(vendedorId);
            return View(pedidos);
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
