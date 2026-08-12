using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using System.Diagnostics;
using Proyecto_Final.Models;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    public class HomeController : Controller
    {
        private readonly AdminDbService _adminDbService;
        private readonly StoreDbService _storeDbService;
        private readonly EmailService _emailService;
        private readonly IComboDbService _combos;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            AdminDbService adminDbService,
            StoreDbService storeDbService,
            EmailService emailService,
            IComboDbService combos,
            ILogger<HomeController> logger)
        {
            _adminDbService = adminDbService;
            _storeDbService = storeDbService;
            _emailService = emailService;
            _combos = combos;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new HomeFeaturedViewModel
            {
                ProductosDestacados = await _adminDbService.GetFeaturedProductsAsync(4)
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Shop(string? categoria, string? buscar)
        {
            var categorias = await _adminDbService.GetStoreCategoriesAsync();
            var productos = await _adminDbService.GetStoreProductsAsync(categoria, buscar);

            var model = new ShopViewModel
            {
                Categoria = categoria,
                Buscar = buscar,
                Categorias = categorias,
                Productos = productos,
                Titulo = string.IsNullOrWhiteSpace(categoria) ? "Tienda" : $"Tienda - {categoria}"
            };

            try
            {
                model.Combos = (await _combos.GetStoreCombosAsync(buscar, HttpContext.RequestAborted)).ToList();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No fue posible cargar combos en la tienda.");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ComboDetail(int id, CancellationToken cancellationToken)
        {
            try
            {
                var combo = await _combos.GetStoreComboAsync(id, cancellationToken);
                if (combo is not null)
                {
                    return View(combo);
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No fue posible cargar el combo {ComboId} en tienda.", id);
            }

            TempData["ErrorMessage"] = "El combo solicitado no está disponible.";
            return RedirectToAction(nameof(Shop));
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var producto = await _storeDbService.GetStoreProductByIdAsync(id);

            if (producto == null)
            {
                TempData["ErrorMessage"] = "El producto solicitado no está disponible.";
                return RedirectToAction(nameof(Shop));
            }

            return View(producto);
        }

        [HttpGet]
        public IActionResult Contact()
        {
            return View(new ContactViewModel
            {
                Nombre = string.Empty,
                Correo = string.Empty,
                Asunto = string.Empty,
                Mensaje = string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await _adminDbService.CreateConsultationAsync(
                    model.Nombre,
                    model.Correo,
                    model.Asunto,
                    model.Mensaje);

                string asunto = $"Consulta Web - {model.Asunto}";

                string contenido = EmailTemplateBuilder.BuildContactNotificationEmail(
                    model.Nombre,
                    model.Correo,
                    model.Asunto,
                    model.Mensaje);

                try
                {
                    _emailService.SendEmail(
                        "p13972127@gmail.com",
                        asunto,
                        contenido);
                }
                catch
                {
                    TempData["ErrorMessage"] = "La consulta fue guardada, pero no fue posible enviar la notificación por correo.";
                    return RedirectToAction(nameof(Contact));
                }

                TempData["SuccessMessage"] = "Tu mensaje fue enviado correctamente.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible registrar la consulta en este momento. Intente nuevamente.";
            }

            return RedirectToAction(nameof(Contact));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [HttpGet]
        public IActionResult Error()
        {
            var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var exception = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
            _logger.LogError(exception, "Solicitud no controlada enviada al manejador global. Ruta {Path}. Solicitud {TraceId}.",
                HttpContext.Request.Path, traceId);
            return View(new ErrorViewModel { RequestId = traceId });
        }
    }
}
