using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [SessionAuthorize("Cliente")]
    public class ClientPortalController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public ClientPortalController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var cliente = await _adminDbService.GetClientDetailAsync(usuarioId);

            if (cliente == null)
            {
                TempData["ErrorMessage"] = "No fue posible cargar la informacion del cliente.";
                return RedirectToAction("Index", "Home");
            }

            var model = new ClientPortalIndexViewModel
            {
                Cliente = MapClientSummary(cliente),
                Credito = await TryGetCreditSummaryAsync(usuarioId),
                Pedidos = cliente.Pedidos.Select(MapOrderListItem).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Statement()
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var cliente = await _adminDbService.GetClientDetailAsync(usuarioId);

            if (cliente == null)
            {
                TempData["ErrorMessage"] = "No fue posible cargar el estado de cuenta.";
                return RedirectToAction(nameof(Index));
            }

            var model = new ClientPortalStatementViewModel
            {
                Cliente = MapClientSummary(cliente),
                Credito = await TryGetCreditSummaryAsync(usuarioId),
                Pedidos = cliente.Pedidos.Select(MapOrderListItem).ToList()
            };

            // Deja registro de auditoria de cada descarga del estado de cuenta.
            await RegistrarAuditoriaAsync(
                "Descargar estado de cuenta",
                "Portal cliente",
                $"El cliente {model.Cliente.NombreCompleto} descargo su estado de cuenta.");

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "No fue posible cargar el pedido solicitado.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var pedido = await _adminDbService.GetOrderDetailAsync(id);

            if (pedido == null || pedido.UsuarioId != usuarioId)
            {
                TempData["ErrorMessage"] = "No fue posible cargar el pedido solicitado.";
                return RedirectToAction(nameof(Index));
            }

            var hasInvoice = await _adminDbService.OrderHasInvoiceAsync(id);
            return View(MapOrderDetail(pedido, hasInvoice));
        }

        [HttpGet]
        [SessionAuthorize("Cliente")]
        public async Task<IActionResult> Invoice(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "No fue posible cargar el comprobante solicitado.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var comprobante = await _adminDbService.GetClientInvoiceByOrderAsync(id, usuarioId);
                if (comprobante == null)
                {
                    TempData["ErrorMessage"] = "No fue posible cargar el comprobante solicitado.";
                    return RedirectToAction(nameof(Index));
                }

                return View(comprobante);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible cargar el comprobante en este momento. Intente nuevamente.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize("Cliente")]
        public async Task<IActionResult> Cancel(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "No fue posible cancelar el pedido solicitado.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var cancelled = await _adminDbService.CancelClientPendingOrderAsync(id, usuarioId);

                if (!cancelled)
                {
                    TempData["ErrorMessage"] = "No fue posible cancelar el pedido solicitado.";
                    return RedirectToAction(nameof(Detail), new { id });
                }

                await RegistrarAuditoriaAsync(
                    "Cancelar pedido",
                    "Portal cliente",
                    $"El cliente cancelo el pedido #{id} desde el portal.");

                TempData["SuccessMessage"] = "Pedido cancelado correctamente.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible cancelar el pedido en este momento. Intente nuevamente.";
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        private async Task<ClientPortalCreditSummaryViewModel?> TryGetCreditSummaryAsync(int usuarioId)
        {
            try
            {
                var credito = await _adminDbService.GetClientCreditDetailAsync(usuarioId);
                if (credito == null)
                {
                    return null;
                }

                return new ClientPortalCreditSummaryViewModel
                {
                    LimiteCredito = credito.LimiteCredito,
                    CreditoActivo = credito.CreditoActivo,
                    CreditoBloqueado = credito.CreditoBloqueado,
                    DeudaActual = credito.DeudaActual,
                    CreditoDisponible = credito.CreditoDisponible,
                    FechaActualizacion = credito.FechaActualizacion
                };
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static ClientPortalSummaryViewModel MapClientSummary(ClientDetailViewModel cliente)
        {
            return new ClientPortalSummaryViewModel
            {
                UsuarioId = cliente.UsuarioId,
                NombreCompleto = cliente.NombreCompleto,
                Correo = cliente.Correo,
                Telefono = cliente.Telefono,
                Direccion = cliente.Direccion,
                Activo = cliente.Activo,
                FechaRegistro = cliente.FechaRegistro,
                TotalPedidos = cliente.TotalPedidos,
                TotalComprado = cliente.TotalComprado,
                UltimoPedido = cliente.UltimoPedido
            };
        }

        private static ClientPortalOrderListItemViewModel MapOrderListItem(ClientOrderSummaryViewModel pedido)
        {
            return new ClientPortalOrderListItemViewModel
            {
                PedidoId = pedido.PedidoId,
                FechaPedido = pedido.FechaPedido,
                Estado = pedido.Estado,
                TipoEntrega = pedido.TipoEntrega,
                DireccionEntrega = pedido.DireccionEntrega,
                Total = pedido.Total,
                Observaciones = pedido.Observaciones
            };
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

        private static ClientPortalOrderDetailViewModel MapOrderDetail(OrderDetailViewModel pedido, bool hasInvoice)
        {
            var canCancel = string.Equals(pedido.Estado, "Pendiente", StringComparison.OrdinalIgnoreCase) && !hasInvoice;

            return new ClientPortalOrderDetailViewModel
            {
                PedidoId = pedido.PedidoId,
                FechaPedido = pedido.FechaPedido,
                Estado = pedido.Estado,
                TipoEntrega = pedido.TipoEntrega,
                DireccionEntrega = pedido.DireccionEntrega,
                Total = pedido.Total,
                Observaciones = pedido.Observaciones,
                HasInvoice = hasInvoice,
                CanCancel = canCancel,
                CancelStatusMessage = canCancel
                    ? "Puede cancelar este pedido mientras permanezca pendiente y sin factura asociada."
                    : "Este pedido no permite cancelacion desde el portal.",
                Lineas = pedido.Detalles.Select(linea => new ClientPortalOrderLineViewModel
                {
                    Producto = linea.Producto,
                    Cantidad = linea.Cantidad,
                    PrecioUnitario = linea.PrecioUnitario,
                    Subtotal = linea.Subtotal
                }).ToList()
            };
        }
    }
}
