using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Facturacion")]
    public class BillingController : Controller
    {
        private readonly AdminDbService _adminDbService;
        private readonly EmailService _emailService;

        public BillingController(AdminDbService adminDbService, EmailService emailService)
        {
            _adminDbService = adminDbService;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _adminDbService.GetSalesReportAsync();

            // M-07 (0336): una factura a crédito con saldo ₡0 se muestra «Pagada» (solo en pantalla).
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<BillingController>>();
            var saldos = (await _adminDbService.TryGetInvoiceBalancesAsync(null, null, logger)).ToDictionary(s => s.FacturaId);
            foreach (var factura in model.Facturas)
            {
                factura.Pagada = saldos.TryGetValue(factura.FacturaId, out var saldo) && saldo.Pagada;
            }

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

            factura.Telefono = await _adminDbService.GetInvoiceClientPhoneAsync(id);
            factura.WhatsAppUrl = WhatsAppReceiptLink.BuildUrl(factura.Telefono, factura.NumeroFactura, factura.FechaFactura, factura.Total);
            ViewBag.FacturaRecienGenerada = Equals(TempData["FacturaRecienGenerada"], id);

            // POST-qa2-03 (CU-093-04): «Vencida» se calcula con el crédito del cliente; Facturas.Estado no cambia.
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<BillingController>>();
            var vencidas = await _adminDbService.TryGetOverdueInvoicesAsync(null, logger);
            ViewBag.FacturaVencida = vencidas.FirstOrDefault(v => v.FacturaId == id);

            // M-07 (0336): con saldo ₡0 la factura se muestra «Pagada»; Facturas.Estado no cambia.
            var saldos = await _adminDbService.TryGetInvoiceBalancesAsync(null, id, logger);
            factura.Pagada = saldos.Any(s => s.FacturaId == id && s.Pagada);

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
                TempData["FacturaRecienGenerada"] = result.FacturaId;
                if (await EnviarComprobantePorCorreoAsync(result.FacturaId))
                {
                    TempData["SuccessMessage"] = $"Factura {result.NumeroFactura} generada correctamente. Se envió el comprobante al correo del cliente.";
                }
                return RedirectToAction(nameof(Detail), new { id = result.FacturaId });
            }
            catch (Exception ex) when (BusinessErrors.IsMissingIdentification(ex))
            {
                // POST-qa2-02 (CU-091-03): sin identificación del cliente no se factura.
                TempData["ErrorMessage"] = BusinessErrors.MissingIdentificationMessage;
                return RedirectToAction("Detail", "OrdersAdmin", new { id = pedidoId });
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible generar la factura del pedido. Verifique que no este cancelado ni facturado previamente.";
                return RedirectToAction("Detail", "OrdersAdmin", new { id = pedidoId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Facturacion", "FACTURACION_GENERAR")]
        public async Task<IActionResult> ResendEmail(int id)
        {
            var enviado = await EnviarComprobantePorCorreoAsync(id);

            TempData[enviado ? "SuccessMessage" : "ErrorMessage"] = enviado
                ? "Comprobante reenviado correctamente al correo del cliente."
                : "No fue posible reenviar el comprobante. Intente nuevamente.";

            return RedirectToAction(nameof(Detail), new { id });
        }

        // Envia el comprobante por correo; no interrumpe el flujo si el envio falla (CU-092).
        private async Task<bool> EnviarComprobantePorCorreoAsync(int facturaId)
        {
            try
            {
                var factura = await _adminDbService.GetInvoiceDetailAsync(facturaId);
                if (factura == null || string.IsNullOrWhiteSpace(factura.Correo))
                {
                    TempData["ErrorMessage"] = "La factura se generó correctamente, pero el cliente no tiene un correo registrado para enviarle el comprobante.";
                    return false;
                }

                var contenido = EmailTemplateBuilder.BuildInvoiceEmail(
                    factura.Cliente,
                    factura.NumeroFactura,
                    factura.PedidoId,
                    factura.FechaFactura,
                    factura.Total);

                _emailService.SendEmail(factura.Correo, $"Comprobante {factura.NumeroFactura}", contenido);

                await RegistrarAuditoriaAsync(
                    "Enviar comprobante por correo",
                    "Facturacion",
                    $"Se envio el comprobante {factura.NumeroFactura} al correo {factura.Correo}.");

                return true;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "La factura se genero correctamente, pero no fue posible enviar el comprobante por correo.";
                return false;
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