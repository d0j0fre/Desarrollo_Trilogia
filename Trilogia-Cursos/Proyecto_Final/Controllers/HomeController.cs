using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Controllers
{
    public class HomeController : Controller
    {
        private readonly AdminDbService _adminDbService;
        private readonly StoreDbService _storeDbService;
        private readonly EmailService _emailService;

        public HomeController(AdminDbService adminDbService, StoreDbService storeDbService, EmailService emailService)
        {
            _adminDbService = adminDbService;
            _storeDbService = storeDbService;
            _emailService = emailService;
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

            return View(model);
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
    }
}
