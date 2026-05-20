using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "CartItems";
        private readonly StoreDbService _storeDbService;

        public CartController(StoreDbService storeDbService)
        {
            _storeDbService = storeDbService;
        }

        public IActionResult Index()
        {
            return View(BuildCartViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productoId, int cantidad = 1)
        {
            var product = await _storeDbService.GetStoreProductByIdAsync(productoId);
            if (product is null)
            {
                TempData["LoginSuccess"] = "El producto no estÃ¡ disponible.";
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
                    TempData["LoginSuccess"] = "Un producto del carrito ya no estÃ¡ disponible.";
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
        public IActionResult Checkout()
        {
            if (!IsLoggedIn())
            {
                TempData["LoginSuccess"] = "Debes iniciar sesiÃ³n para finalizar la compra.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Checkout), "Cart") });
            }

            var cart = BuildCartViewModel();
            if (cart.Items.Count == 0)
            {
                TempData["LoginSuccess"] = "Tu carrito estÃ¡ vacÃ­o.";
                return RedirectToAction(nameof(Index));
            }

            var model = new CheckoutViewModel
            {
                Cart = cart,
                CorreoElectronico = HttpContext.Session.GetString("UserEmail"),
                TipoEntrega = "EnvÃ­o a domicilio"
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            model.Cart = BuildCartViewModel();
            model.TipoEntrega = "EnvÃ­o a domicilio";

            if (!IsLoggedIn())
            {
                TempData["LoginSuccess"] = "Debes iniciar sesiÃ³n para finalizar la compra.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Checkout), "Cart") });
            }

            if (model.Cart.Items.Count == 0)
            {
                TempData["LoginSuccess"] = "Tu carrito estÃ¡ vacÃ­o.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.Provincia))
                ModelState.AddModelError(nameof(model.Provincia), "La provincia es obligatoria.");
            if (string.IsNullOrWhiteSpace(model.Canton))
                ModelState.AddModelError(nameof(model.Canton), "El cantÃ³n es obligatorio.");
            if (string.IsNullOrWhiteSpace(model.Distrito))
                ModelState.AddModelError(nameof(model.Distrito), "El distrito es obligatorio.");
            if (string.IsNullOrWhiteSpace(model.DireccionDetalle))
                ModelState.AddModelError(nameof(model.DireccionDetalle), "La direcciÃ³n es obligatoria.");
            if (string.IsNullOrWhiteSpace(model.Identificacion))
                ModelState.AddModelError(nameof(model.Identificacion), "La identificaciÃ³n es obligatoria.");

            model.DireccionEntrega = $"{model.Pais}, {model.Provincia}, {model.Canton}, {model.Distrito}. {model.DireccionDetalle}";

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var pedidoId = await _storeDbService.CreateOrderAsync(HttpContext.Session.GetInt32("UserId") ?? 0, model, model.Cart.Items);
                HttpContext.Session.Remove(CartSessionKey);

                TempData["OrderConfirmation"] = JsonSerializer.Serialize(new OrderConfirmationViewModel
                {
                    PedidoId = pedidoId,
                    TipoEntrega = model.TipoEntrega,
                    DireccionEntrega = model.DireccionEntrega,
                    Total = model.Cart.Subtotal,
                    Items = model.Cart.Items
                });

                TempData["LoginSuccess"] = $"Pedido #{pedidoId} creado correctamente.";
                return RedirectToAction(nameof(Confirmation));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
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

        private CartViewModel BuildCartViewModel()
        {
            var items = GetCartItems();
            return new CartViewModel { Items = items };
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

