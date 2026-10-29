using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Pedidos", "PEDIDOS_AUTORIZAR_RECHAZAR", allowAdministratorBypass: false)]
    [CommercialAssignmentsRequired]
    public class ManagerOrdersController : Controller
    {
        private readonly ICommercialOrderService _orders;
        private readonly ILogger<ManagerOrdersController> _logger;

        public ManagerOrdersController(ICommercialOrderService orders, ILogger<ManagerOrdersController> logger)
        {
            _orders = orders;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? buscar)
        {
            ViewBag.Buscar = buscar;
            var pedidos = await _orders.RetainedAsync(buscar, HttpContext.RequestAborted);
            return View(pedidos);
        }

        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            (OrderDetailViewModel Order, long Revision)? detail;
            try { detail = await _orders.DetailsAsync(id, true, HttpContext.RequestAborted); }
            catch (Microsoft.Data.SqlClient.SqlException error) when (error.Number is 58702 or 58908) { return NotFound(); }
            var pedido = detail?.Order;

            if (pedido == null)
            {
                TempData["ErrorMessage"] = "No se encontró el pedido solicitado.";
                return RedirectToAction(nameof(Index));
            }

            if (pedido.Estado != "Retenido")
            {
                TempData["ErrorMessage"] = "Solo se pueden revisar pedidos en estado Retenido.";
                return RedirectToAction(nameof(Index));
            }

            var operationId = Guid.NewGuid();
            var vm = new ManagerOrderReviewViewModel
            {
                OperationId = operationId,
                ExpectedRevision = detail!.Value.Revision,
                Pedido       = pedido,
                RechazarForm = new ManagerRejectOrderViewModel { PedidoId = id, OperationId = operationId, ExpectedRevision = detail.Value.Revision }
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(128 * 1024)]
        public async Task<IActionResult> Approve(int pedidoId, Guid operationId, long expectedRevision)
        {
            if (pedidoId <= 0 || operationId == Guid.Empty || expectedRevision <= 0)
            {
                TempData["ErrorMessage"] = "La solicitud de aprobación no es válida.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioId     = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Gerente";

            try
            {
                var result = await _orders.DecideAsync(pedidoId, operationId, expectedRevision, true, null, HttpContext.RequestAborted);

                TempData["Confirm_PedidoId"]      = result.PedidoId;
                TempData["Confirm_NumeroFactura"]  = result.NumeroFactura;
                return RedirectToAction(nameof(ApproveConfirmation));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo aprobar el pedido retenido {OrderId}; código {Code}.", pedidoId, (ex as Microsoft.Data.SqlClient.SqlException)?.Number);
                TempData["ErrorMessage"] = "No fue posible aprobar el pedido en este momento.";
                return RedirectToAction(nameof(Review), new { id = pedidoId });
            }
        }

        [HttpGet]
        public IActionResult ApproveConfirmation()
        {
            var pedidoId      = TempData["Confirm_PedidoId"]     as int?    ?? 0;
            var numeroFactura = TempData["Confirm_NumeroFactura"] as string  ?? string.Empty;

            if (pedidoId <= 0)
                return RedirectToAction(nameof(Index));

            var vm = new ManagerApproveResultViewModel
            {
                PedidoId      = pedidoId,
                Estado        = "Facturado",
                NumeroFactura = numeroFactura
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(128 * 1024)]
        public async Task<IActionResult> Reject([Bind(Prefix = "RechazarForm")] ManagerRejectOrderViewModel model)
        {
            if (model.OperationId == Guid.Empty || model.ExpectedRevision <= 0)
                ModelState.AddModelError(string.Empty, "La revisión no es válida. Volvé a abrir el pedido antes de decidir.");
            if (!ModelState.IsValid)
            {
                (OrderDetailViewModel Order, long Revision)? detail;
                try { detail = await _orders.DetailsAsync(model.PedidoId, true, HttpContext.RequestAborted); }
                catch (Microsoft.Data.SqlClient.SqlException error) when (error.Number is 58702 or 58908) { return NotFound(); }
                var pedido = detail?.Order;
                if (pedido == null) return RedirectToAction(nameof(Index));

                var vm = new ManagerOrderReviewViewModel { Pedido = pedido, RechazarForm = model,
                    OperationId = model.OperationId, ExpectedRevision = model.ExpectedRevision };
                return View("Review", vm);
            }

            var usuarioId     = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Gerente";

            try
            {
                await _orders.DecideAsync(model.PedidoId, model.OperationId, model.ExpectedRevision, false,
                    model.MotivoRechazo, HttpContext.RequestAborted);

                TempData["SuccessMessage"] = $"Pedido #{model.PedidoId} rechazado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo rechazar el pedido retenido {OrderId}; código {Code}.", model.PedidoId, (ex as Microsoft.Data.SqlClient.SqlException)?.Number);
                TempData["ErrorMessage"] = "No fue posible rechazar el pedido en este momento.";
                return RedirectToAction(nameof(Review), new { id = model.PedidoId });
            }
        }

    }
}
