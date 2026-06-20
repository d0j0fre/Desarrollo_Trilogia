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
                TempData["ErrorMessage"] = "No se encontro la factura solicitada.";
                return RedirectToAction(nameof(Index));
            }

            return View(factura);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Facturacion", "FACTURACION_GENERAR")]
        public async Task<IActionResult> GenerateFromOrder(int pedidoId)
        {
            if (pedidoId <= 0)
            {
                TempData["ErrorMessage"] = "No fue posible generar la factura del pedido solicitado.";
                return RedirectToAction("Index", "OrdersAdmin");
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                var result = await _adminDbService.GenerateInvoiceFromOrderAsync(pedidoId, usuarioId, usuarioNombre);

                await RegistrarAuditoriaAsync(
                    "Generar factura",
                    "Facturacion",
                    $"Se genero la factura {result.NumeroFactura} para el pedido #{pedidoId}.");

                TempData["SuccessMessage"] = $"Factura {result.NumeroFactura} generada correctamente.";
                return RedirectToAction(nameof(Detail), new { id = result.FacturaId });
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible generar la factura del pedido. Verifique que no este cancelado ni facturado previamente.";
                return RedirectToAction("Detail", "OrdersAdmin", new { id = pedidoId });
            }
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
