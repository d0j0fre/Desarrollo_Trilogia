using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "CartItems";
        private static readonly HashSet<string> PaymentMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "Efectivo contra entrega",
            "SINPE Móvil simulado",
            "Tarjeta demo",
            "Transferencia simulada"
        };

        private readonly StoreDbService _storeDbService;
        private readonly EmailService _emailService;
        private readonly PromotionsDbService _promotions;
        private readonly ILogger<CartController> _logger;

        public CartController(
            StoreDbService storeDbService,
            EmailService emailService,
            PromotionsDbService promotions,
            ILogger<CartController> logger)
        {
            _storeDbService = storeDbService;
            _emailService = emailService;
            _promotions = promotions;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            return View(await BuildCartViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productoId, int cantidad = 1)
        {
            var product = await _storeDbService.GetStoreProductByIdAsync(productoId);
            if (product is null)
            {
                TempData["LoginSuccess"] = "El producto no está disponible.";
                return RedirectToAction("Shop", "Home");
            }

            var items = GetCartItems();
            var item = items.FirstOrDefault(x => x.ProductoId == productoId);
            if (item is null)
            {
                items.Add(new CartItemViewModel
                {
                    ProductoId = product.ProductoId,
                    Nombre = product.Nombre,
                    Categoria = product.Categoria,
                    Descripcion = product.Descripcion,
                    Precio = product.Precio,
                    StockDisponible = product.Stock,
                    Cantidad = Math.Min(Math.Max(cantidad, 1), Math.Max(product.Stock, 1)),
                    ImagenUrl = product.ImagenUrl
                });
            }
            else
            {
                item.StockDisponible = product.Stock;
                item.Precio = product.Precio;
                item.Cantidad = Math.Min(item.Cantidad + Math.Max(cantidad, 1), Math.Max(product.Stock, 1));
            }

            SaveCartItems(items);
            TempData["LoginSuccess"] = $"{product.Nombre} fue agregado al carrito.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int productoId, int cantidad)
        {
            var items = GetCartItems();
            var item = items.FirstOrDefault(x => x.ProductoId == productoId);
            if (item is null)
                return RedirectToAction(nameof(Index));

            if (cantidad <= 0)
            {
                items.Remove(item);
            }
            else
            {
                var product = await _storeDbService.GetStoreProductByIdAsync(productoId);
                if (product is null)
                {
                    items.Remove(item);
                    TempData["LoginSuccess"] = "Un producto del carrito ya no está disponible.";
                }
                else
                {
                    item.StockDisponible = product.Stock;
                    item.Precio = product.Precio;
                    item.Cantidad = Math.Min(cantidad, Math.Max(product.Stock, 1));
                }
            }

            SaveCartItems(items);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int productoId)
        {
            var items = GetCartItems();
            items.RemoveAll(x => x.ProductoId == productoId);
            SaveCartItems(items);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            if (!IsLoggedIn())
            {
                TempData["LoginSuccess"] = "Debes iniciar sesión para finalizar la compra.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Checkout), "Cart") });
            }

            var cart = await BuildCartViewModelAsync();
            if (cart.Items.Count == 0)
            {
                TempData["LoginSuccess"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Index));
            }

            var model = new CheckoutViewModel
            {
                Cart = cart,
                CorreoElectronico = HttpContext.Session.GetString("UserEmail"),
                TipoEntrega = "Envío a domicilio",
                MetodoPago = "Efectivo contra entrega"
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            model.Cart = await BuildCartViewModelAsync();
            model.TipoEntrega = "Envío a domicilio";

            model.MetodoPago = string.IsNullOrWhiteSpace(model.MetodoPago)
                ? "Efectivo contra entrega"
                : model.MetodoPago.Trim();

            model.ReferenciaPago = string.IsNullOrWhiteSpace(model.ReferenciaPago)
                ? null
                : model.ReferenciaPago.Trim();

            if (!IsLoggedIn())
            {
                TempData["LoginSuccess"] =
                    "Debes iniciar sesión para finalizar la compra.";

                return RedirectToAction(
                    "Login",
                    "Account",
                    new
                    {
                        returnUrl = Url.Action(nameof(Checkout), "Cart")
                    });
            }

            if (model.Cart.Items.Count == 0)
            {
                TempData["LoginSuccess"] = "Tu carrito está vacío.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.Provincia))
            {
                ModelState.AddModelError(
                    nameof(model.Provincia),
                    "La provincia es obligatoria.");
            }

            if (string.IsNullOrWhiteSpace(model.Canton))
            {
                ModelState.AddModelError(
                    nameof(model.Canton),
                    "El cantón es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(model.Distrito))
            {
                ModelState.AddModelError(
                    nameof(model.Distrito),
                    "El distrito es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(model.DireccionDetalle))
            {
                ModelState.AddModelError(
                    nameof(model.DireccionDetalle),
                    "La dirección es obligatoria.");
            }

            if (string.IsNullOrWhiteSpace(model.Identificacion))
            {
                ModelState.AddModelError(
                    nameof(model.Identificacion),
                    "La identificación es obligatoria.");
            }

            if (!PaymentMethods.Contains(model.MetodoPago))
            {
                ModelState.AddModelError(
                    nameof(model.MetodoPago),
                    "Seleccione un método de pago válido.");
            }

            model.CorreoElectronico =
                string.IsNullOrWhiteSpace(model.CorreoElectronico)
                    ? HttpContext.Session.GetString("UserEmail")
                    : model.CorreoElectronico.Trim();

            if (string.IsNullOrWhiteSpace(model.CorreoElectronico))
            {
                ModelState.AddModelError(
                    nameof(model.CorreoElectronico),
                    "No se encontró un correo electrónico para enviar el comprobante.");
            }

            model.DireccionEntrega =
                $"{model.Pais}, {model.Provincia}, {model.Canton}, " +
                $"{model.Distrito}. {model.DireccionDetalle}";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var usuarioId =
                    HttpContext.Session.GetInt32("UserId") ?? 0;

                var order = await _storeDbService.CreateOrderWithPromotionsAsync(
                    usuarioId,
                    model,
                    model.Cart.Items);

                var confirmedItems = model.Cart.Items.Concat(order.Gifts).ToList();

                var confirmacion = new OrderConfirmationViewModel
                {
                    PedidoId = order.PedidoId,
                    TipoEntrega = model.TipoEntrega,
                    DireccionEntrega = model.DireccionEntrega,
                    Total = order.Total,
                    Items = confirmedItems
                };

                var destinatario =
                    model.CorreoElectronico
                    ?? HttpContext.Session.GetString("UserEmail")
                    ?? string.Empty;

                var cliente =
                    HttpContext.Session.GetString("UserFullName")
                    ?? "Cliente";

                _emailService.SendOrderReceipt(
                    destinatario,
                    cliente,
                    order.PedidoId,
                    model,
                    confirmedItems);

                HttpContext.Session.Remove(CartSessionKey);

                TempData["OrderConfirmation"] =
                    JsonSerializer.Serialize(confirmacion);

                TempData["LoginSuccess"] =
                    $"Pedido #{order.PedidoId} creado correctamente.";

                return RedirectToAction(nameof(Confirmation));
            }
            catch (SqlException ex)
                when (ex.Message.Contains(
                    "stock",
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "El checkout fue rechazado por falta de inventario para el usuario {UserId}.", HttpContext.Session.GetInt32("UserId"));
                ModelState.AddModelError(
                    string.Empty,
                    "No hay stock suficiente para completar el pedido. " +
                    "Revise el carrito e intente nuevamente.");

                return View(model);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No se pudo completar el checkout del usuario {UserId}.", HttpContext.Session.GetInt32("UserId"));
                ModelState.AddModelError(
                    string.Empty,
                    "No se pudo completar el pedido. Intente nuevamente.");

                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Confirmation()
        {
            if (TempData["OrderConfirmation"] is not string raw || string.IsNullOrWhiteSpace(raw))
                return RedirectToAction(nameof(Index));

            var model = JsonSerializer.Deserialize<OrderConfirmationViewModel>(raw);
            return View(model ?? new OrderConfirmationViewModel());
        }

        private bool IsLoggedIn() => !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("UserEmail"));

        // CU-173 — arma el carrito y aplica automáticamente las promociones vigentes.
        private async Task<CartViewModel> BuildCartViewModelAsync()
        {
            var items = await RefreshCartItemsAsync(GetCartItems());
            SaveCartItems(items);
            var cart = new CartViewModel { Items = items };
            if (items.Count == 0) return cart;

            // Mejor esfuerzo: si el motor de promociones falla, se muestra el carrito sin promociones.
            try
            {
                var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var segmento = await _storeDbService.GetUserSegmentAsync(usuarioId);
                var vigentes = await _promotions.GetActivePromotionsAsync(segmento);
                var resultado = PromotionEngine.Apply(cart.Items, vigentes);
                cart.Regalias = resultado.Gifts;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No fue posible calcular las promociones para presentar el carrito.");
                foreach (var it in cart.Items) { it.MontoDescuento = 0; it.PromocionNombre = null; }
                cart.Regalias.Clear();
            }
            return cart;
        }

        private async Task<List<CartItemViewModel>> RefreshCartItemsAsync(List<CartItemViewModel> items)
        {
            var refreshed = new List<CartItemViewModel>();
            foreach (var item in items.Where(item => item.ProductoId > 0 && item.Cantidad > 0))
            {
                var product = await _storeDbService.GetStoreProductByIdAsync(item.ProductoId);
                if (product is null || product.Stock <= 0) continue;
                refreshed.Add(new CartItemViewModel
                {
                    ProductoId = product.ProductoId,
                    Nombre = product.Nombre,
                    Categoria = product.Categoria,
                    Descripcion = product.Descripcion,
                    Precio = product.Precio,
                    StockDisponible = product.Stock,
                    Cantidad = Math.Min(item.Cantidad, product.Stock),
                    ImagenUrl = product.ImagenUrl
                });
            }
            return refreshed;
        }

        private List<CartItemViewModel> GetCartItems()
        {
            var json = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrWhiteSpace(json))
                return new List<CartItemViewModel>();

            return JsonSerializer.Deserialize<List<CartItemViewModel>>(json) ?? new List<CartItemViewModel>();
        }

        private void SaveCartItems(List<CartItemViewModel> items)
        {
            HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(items));
        }
    }
}

