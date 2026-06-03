using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Facturacion")]
    public class BillingController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public BillingController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _adminDbService.GetSalesReportAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var factura = await _adminDbService.GetInvoiceDetailAsync(id);

            if (factura == null)
            {
                TempData["ErrorMessage"] = "No se encontró la factura solicitada.";
                return RedirectToAction(nameof(Index));
            }

            return View(factura);
        }
    }
}