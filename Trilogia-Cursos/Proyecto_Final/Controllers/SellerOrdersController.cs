using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;
using Proyecto_Final.Validation;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Venta móvil", "VENTA_MOVIL_CREAR", false)]
    public class SellerOrdersController : Controller
    {
        private readonly AdminDbService _adminDbService;
        private readonly ICommercialOrderService _orders;

        public SellerOrdersController(AdminDbService adminDbService, ICommercialOrderService orders)
        {
            _adminDbService = adminDbService;
            _orders = orders;
        }

        [HttpGet]
        [CommercialAssignmentsRequired]
        public async Task<IActionResult> Index()
        {
            var model = await BuildCreateModelAsync(new SellerOrderCreateViewModel { RequestId = Guid.NewGuid() });
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, CommercialAssignmentsRequired]
        [RequestSizeLimit(128 * 1024)]
        public async Task<IActionResult> Review(SellerOrderCreateViewModel model)
        {
            if (model.RequestId == Guid.Empty) ModelState.AddModelError(nameof(model.RequestId), "El intento del pedido no es válido. Volvé a Mis pedidos antes de iniciar otro.");
            if (!model.Productos.Any(x => x.Cantidad > 0)) ModelState.AddModelError(nameof(model.Productos), "Seleccioná al menos un producto.");
            if (!ModelState.IsValid) return View("Index", await BuildCreateModelAsync(model));
            try
            {
                var quote = await _orders.ReviewAsync(model, HttpContext.RequestAborted);
                var clients = await _orders.ClientsAsync(HttpContext.RequestAborted);
                var customer = clients.SingleOrDefault(x => x.UsuarioId == model.ClienteUsuarioId);
                if (customer is null) return NotFound();
                model.QuoteFingerprint = quote.IntentFingerprint;
                model.QuoteSourceHash = quote.SourceHash;
                return View(new SellerOrderReviewViewModel(model, quote, customer.NombreCompleto));
            }
            catch (UnauthorizedAccessException) { return Unauthorized(); }
            catch (CommercialQuoteTransportException error) when (error.Status == 401) { return Unauthorized(); }
            catch (Microsoft.Data.SqlClient.SqlException error) when (error.Number == 56801) { return Unauthorized(); }
            catch (CommercialQuoteTransportException error) when (error.Status == 403) { return StatusCode(403); }
            catch (Microsoft.Data.SqlClient.SqlException error) when (error.Number == 56802) { return StatusCode(403); }
            catch (Exception error) when (error is ArgumentException or CommercialQuoteTransportException or Microsoft.Data.SqlClient.SqlException or HttpRequestException or InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo confirmar el total final. Se conserva este intento: revisá Mis pedidos o reintentá con los mismos datos.");
                return View("Index", await BuildCreateModelAsync(model));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CommercialAssignmentsRequired]
        [RequestSizeLimit(128 * 1024)]
        public async Task<IActionResult> Create(SellerOrderCreateViewModel model)
        {
            if (model.RequestId == Guid.Empty) ModelState.AddModelError(nameof(model.RequestId), "Falta el intento original del pedido.");
            if (string.IsNullOrEmpty(model.QuoteFingerprint) || string.IsNullOrEmpty(model.QuoteSourceHash))
                ModelState.AddModelError(string.Empty, "Revisá el total final antes de confirmar el pedido.");
            model.IdentificacionCliente = CostaRicanIdentificationAttribute.Normalize(model.IdentificacionCliente);

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
                // SQL commits order, monetary snapshot, stock, invoice and audit together.
                // A later non-transactional audit must not turn a committed result into failure.
                var result = await _orders.CreateAsync(model, false, HttpContext.RequestAborted);

                if (result.EsRetenido)
                    return RedirectToAction(nameof(RetainedConfirmation), new { id = result.PedidoId });

                TempData["ConfirmNumeroFactura"] = result.NumeroFactura;
                return RedirectToAction(nameof(Confirmation), new { id = result.PedidoId });
            }
            catch (CommercialQuoteTransportException error) when (error.Status == 401) { return Unauthorized(); }
            catch (Microsoft.Data.SqlClient.SqlException error) when (error.Number == 56801) { return Unauthorized(); }
            catch (CommercialQuoteTransportException error) when (error.Status == 403) { return StatusCode(403); }
            catch (Microsoft.Data.SqlClient.SqlException error) when (error.Number == 56802) { return StatusCode(403); }
            catch (Microsoft.Data.SqlClient.SqlException error) when (error.Number == 58906)
            {
                ModelState.AddModelError(string.Empty, "El catálogo cambió antes de registrar. Revisá Mis pedidos y abrí una nueva selección para actualizar el total.");
                Response.StatusCode = 409;
                return View("Index", await BuildCreateModelAsync(model));
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "No fue posible registrar el pedido en este momento. Intente nuevamente.");
                return View("Index", await BuildCreateModelAsync(model));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CommercialAssignmentsRequired]
        [RequestSizeLimit(128 * 1024)]
        public async Task<IActionResult> SyncOffline([FromBody] SellerOfflineOrderSyncRequestViewModel request)
        {
            if (request == null || request.Productos is null || request.Productos.Count > 100 || !ModelState.IsValid)
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
                RequestId = pedidoOfflineGuid,
                ClienteUsuarioId = request.ClienteUsuarioId,
                TipoEntrega = request.TipoEntrega.Trim(),
                DireccionEntrega = request.DireccionEntrega?.Trim(),
                IdentificacionCliente = string.IsNullOrWhiteSpace(request.IdentificacionCliente) ? null : CostaRicanIdentificationAttribute.Normalize(request.IdentificacionCliente),
                Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
                Productos = productosSeleccionados
            };

            try
            {
                var result = await _orders.CreateAsync(model, true, HttpContext.RequestAborted);

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
            var vendedorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (!await _adminDbService.SellerOwnsOrderAsync(id, vendedorId))
                return NotFound();

            var pedido = _orders.Enabled ? (await _orders.DetailsAsync(id, false, HttpContext.RequestAborted))?.Order
                : await _adminDbService.GetOrderDetailAsync(id);
            if (pedido == null)
                return RedirectToAction(nameof(Index));

            var factura = _orders.Enabled ? null : await _adminDbService.GetInvoiceSummaryByOrderAsync(id);
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
            var vendedorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (!await _adminDbService.SellerOwnsOrderAsync(id, vendedorId))
                return NotFound();

            var pedido = _orders.Enabled ? (await _orders.DetailsAsync(id, false, HttpContext.RequestAborted))?.Order
                : await _adminDbService.GetOrderDetailAsync(id);
            if (pedido == null)
                return RedirectToAction(nameof(Index));

            return View(pedido);
        }

        [HttpGet]
        public async Task<IActionResult> MyOrders()
        {
            var vendedorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var pedidos = _orders.Enabled ? await _orders.HistoryAsync(HttpContext.RequestAborted)
                : await _adminDbService.GetSellerMyOrdersAsync(vendedorId);
            return View(pedidos);
        }

        private async Task<SellerOrderCreateViewModel> BuildCreateModelAsync(SellerOrderCreateViewModel model)
        {
            model.Clientes = await _orders.ClientsAsync(HttpContext.RequestAborted);

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
