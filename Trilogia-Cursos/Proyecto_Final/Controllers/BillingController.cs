using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Facturacion")]
    public class BillingController : Controller
    {
        private readonly AdminDbService _adminDbService;
        private readonly IInvoiceNotificationClient _notifications;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BillingController> _logger;

        public BillingController(AdminDbService adminDbService, IInvoiceNotificationClient notifications, IConfiguration configuration, ILogger<BillingController> logger)
        {
            _adminDbService = adminDbService;
            _logger = logger;
            _notifications = notifications;
            _configuration = configuration;
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

            ViewData["MailState"] = "Unavailable";
            if (_configuration.GetValue<bool>("InvoiceNotifications:Enabled"))
            {
                try
                {
                    var login = OriginalLoginSession.ValidatedRequest(HttpContext);
                    var receipt = await _notifications.ReceiptAsync(id, Guid.Empty, HttpContext.RequestAborted);
                    var unsubmitted = InvoiceNotificationAttempt.Peek(HttpContext.Session, login, id);
                    // If the unsubmitted UUID reached SQL, the durable receipt takes precedence.
                    if (unsubmitted == receipt.OperationId)
                    { InvoiceNotificationAttempt.Confirmed(HttpContext.Session, id); unsubmitted = null; }
                    if (receipt.State == "None" || unsubmitted.HasValue)
                    {
                        ViewData["MailOperation"] = unsubmitted ?? InvoiceNotificationAttempt.ForInvoice(HttpContext.Session, login, id);
                        ViewData["MailState"] = "Ready";
                    }
                    else
                    {
                        ViewData["MailOperation"] = receipt.OperationId;
                        ViewData["MailState"] = receipt.State;
                    }
                }
                catch (Exception e) when (e is InvoiceNotificationTransportException or HttpRequestException or OperationCanceledException or InvalidOperationException)
                { LogNotification(e); }
            }
            return View(factura);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Facturacion", "FACTURACION_GENERAR", false)]
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
                var result = await _adminDbService.GenerateInvoiceFromOrderAsync(pedidoId, usuarioId, usuarioNombre, OriginalLoginSession.ValidatedRequest(HttpContext));

                // The SQL workflow commits invoice, states and canonical audit together.

                TempData["SuccessMessage"] = $"Factura {result.NumeroFactura} generada correctamente.";
                if (_configuration.GetValue<bool>("InvoiceNotifications:Enabled"))
                {
                    try
                    {
                        var receipt = await _notifications.SendAsync(result.FacturaId, Guid.NewGuid(), true, HttpContext.RequestAborted);
                        TempData["InfoMessage"] = NotificationMessage(receipt.State);
                    }
                    catch (Exception e)
                    { LogNotification(e); TempData["ErrorMessage"] = "La factura está emitida. El correo no quedó confirmado; consulte su estado antes de solicitar otra copia."; }
                }
                return RedirectToAction(nameof(Detail), new { id = result.FacturaId });
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Operación rechazada. Tipo {ExceptionType}; código SQL {SqlError}.", exception.GetType().Name, exception is Microsoft.Data.SqlClient.SqlException sql ? sql.Number : (int?)null);
                TempData["ErrorMessage"] = "No fue posible generar la factura del pedido. Verifique que no este cancelado ni facturado previamente.";
                return RedirectToAction("Detail", "OrdersAdmin", new { id = pedidoId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Facturacion", "FACTURACION_GENERAR", false)]
        public async Task<IActionResult> ResendEmail(int id, Guid operationId)
        {
            if (id <= 0 || operationId == Guid.Empty) return BadRequest();
            var login = OriginalLoginSession.ValidatedRequest(HttpContext);
            try
            {
                if (!InvoiceNotificationAttempt.Matches(HttpContext.Session, login, id, operationId))
                    _ = await _notifications.ReceiptAsync(id, operationId, HttpContext.RequestAborted);
                var receipt = await _notifications.SendAsync(id, operationId, false, HttpContext.RequestAborted);
                InvoiceNotificationAttempt.Confirmed(HttpContext.Session, id);
                TempData[receipt.State == "SmtpAccepted" ? "SuccessMessage" : "ErrorMessage"] = NotificationMessage(receipt.State);
            }
            catch (Exception e) when (e is InvoiceNotificationTransportException or HttpRequestException or OperationCanceledException)
            {
                LogNotification(e);
                TempData["ErrorMessage"] = "El correo no quedó confirmado. Consulte el estado del mismo intento; no solicite otra copia a ciegas.";
            }
            return RedirectToAction(nameof(Detail), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminAuthorize("Facturacion", "FACTURACION_GENERAR", false)]
        public async Task<IActionResult> NewEmailAttempt(int id, Guid previousOperationId)
        {
            if (id <= 0 || previousOperationId == Guid.Empty) return BadRequest();
            try
            {
                var receipt = await _notifications.ReceiptAsync(id, Guid.Empty, HttpContext.RequestAborted);
                if (receipt.OperationId != previousOperationId || receipt.State != "SmtpAccepted") return Conflict();
                var login = OriginalLoginSession.ValidatedRequest(HttpContext);
                if (InvoiceNotificationAttempt.Matches(HttpContext.Session, login, id, receipt.OperationId))
                    InvoiceNotificationAttempt.Confirmed(HttpContext.Session, id);
                // Double-clicks prepare the same unsubmitted UUID. This endpoint never sends mail.
                _ = InvoiceNotificationAttempt.ForInvoice(HttpContext.Session, login, id);
            }
            catch (Exception e) when (e is InvoiceNotificationTransportException or HttpRequestException or OperationCanceledException or InvalidOperationException)
            {
                LogNotification(e);
                TempData["ErrorMessage"] = "No fue posible preparar otra copia. Verifique el estado del envío anterior.";
            }
            return RedirectToAction(nameof(Detail), new { id });
        }

        private void LogNotification(Exception e) => _logger.LogWarning("Invoice notification unconfirmed. Type {Type}; status {Status}.",
            e.GetType().Name, (e as InvoiceNotificationTransportException)?.Status);

        private static string NotificationMessage(string state) => state switch
        {
            "SmtpAccepted" => "El servidor de correo aceptó el comprobante. Esto no acredita su entrega al buzón.",
            "AlreadyRequested" => "Ya existe una solicitud inicial para esta factura. No se envió otra copia.",
            "Pending" => "Solicitud registrada. Continúe con el mismo intento para enviar el comprobante.",
            "Sending" => "Envío en curso. Consulte su estado antes de solicitar otra copia.",
            _ => "Resultado de correo desconocido. No se reintentará automáticamente para evitar duplicados."
        };
    }
}
