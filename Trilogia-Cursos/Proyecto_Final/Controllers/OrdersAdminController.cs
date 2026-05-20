using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize]
    public class OrdersAdminController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public OrdersAdminController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? estado)
        {
            ViewBag.Estado = estado;

            var pedidos = await _adminDbService.GetOrdersAsync(estado);
            return View(pedidos);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var pedido = await _adminDbService.GetOrderDetailAsync(id);

            if (pedido == null)
            {
                TempData["ErrorMessage"] = "No se encontró el pedido solicitado.";
                return RedirectToAction(nameof(Index));
            }

            return View(pedido);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int pedidoId, string nuevoEstado)
        {
            if (pedidoId <= 0 || string.IsNullOrWhiteSpace(nuevoEstado))
            {
                TempData["ErrorMessage"] = "La solicitud para actualizar el estado no es válida.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                await _adminDbService.UpdateOrderStatusAsync(pedidoId, nuevoEstado.Trim(), usuarioId, usuarioNombre);
                TempData["SuccessMessage"] = "Estado del pedido actualizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Detail), new { id = pedidoId });
        }
    }
}