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

            return View(MapOrderDetail(pedido));
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

        private static ClientPortalOrderDetailViewModel MapOrderDetail(OrderDetailViewModel pedido)
        {
            return new ClientPortalOrderDetailViewModel
            {
                PedidoId = pedido.PedidoId,
                FechaPedido = pedido.FechaPedido,
                Estado = pedido.Estado,
                TipoEntrega = pedido.TipoEntrega,
                DireccionEntrega = pedido.DireccionEntrega,
                Total = pedido.Total,
                Observaciones = pedido.Observaciones,
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
