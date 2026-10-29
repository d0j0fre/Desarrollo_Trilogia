using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-081 (rutas de entrega) + CU-083 E3 (consulta de evidencias por ruta).
    [AdminAuthorize("Rutas", "RUTAS_GESTIONAR", allowAdministratorBypass: false)]
    public class RoutesAdminController : Controller
    {
        private readonly LogisticsDbService _logistics;
        private readonly AdminDbService _adminDbService;
        private readonly IRouteDispatchNotificationClient _notifications;
        private readonly ILogger<RoutesAdminController> _logger;

        public RoutesAdminController(
            LogisticsDbService logistics,
            AdminDbService adminDbService,
            IRouteDispatchNotificationClient notifications,
            ILogger<RoutesAdminController> logger)
        {
            _logistics = logistics;
            _adminDbService = adminDbService;
            _notifications = notifications;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? estado, string? buscar)
        {
            ViewBag.Estado = estado;
            ViewBag.Buscar = buscar;
            var rutas = await _logistics.GetRoutesAsync(estado, buscar);
            return View(rutas);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = await BuildCreateViewModelAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RouteCreateViewModel model)
        {
            model.PedidosSeleccionados ??= new List<int>();

            if (model.PedidosSeleccionados.Count == 0)
            {
                ModelState.AddModelError(nameof(model.PedidosSeleccionados), "Debe seleccionar al menos un pedido para la ruta.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCreateOptionsAsync(model);
                return View(model);
            }

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                var (rutaId, codigo) = await _logistics.CreateRouteAsync(
                    model.Zona, model.ChoferUsuarioId, model.VehiculoId, model.Observaciones,
                    model.PedidosSeleccionados, usuarioId, usuarioNombre);

                TempData["SuccessMessage"] = $"Ruta {codigo} creada correctamente.";
                return RedirectToAction(nameof(Detail), new { id = rutaId });
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                ModelState.AddModelError(string.Empty, "No fue posible completar la operación solicitada.");
                await PopulateCreateOptionsAsync(model);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear ruta de entrega.");
                ModelState.AddModelError(string.Empty, "No fue posible crear la ruta en este momento.");
                await PopulateCreateOptionsAsync(model);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id, Guid? dispatchId = null)
        {
            var cabecera = await _logistics.GetRouteHeaderAsync(id);
            if (cabecera == null)
            {
                TempData["ErrorMessage"] = "No se encontró la ruta solicitada.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new RouteDetailViewModel
            {
                Cabecera = cabecera,
                Pedidos = await _logistics.GetRouteOrdersAsync(id),
                DispatchOperationId = cabecera.Estado == "Planificada" ? Guid.NewGuid() : Guid.Empty
            };

            if (dispatchId is { } original && original != Guid.Empty)
            {
                try
                {
                    vm.DispatchNotifications = await _logistics.GetOwnedRouteDispatchAsync(id, original, HttpContext.RequestAborted);
                    vm.DispatchOperationId = original;
                }
                catch (Exception error) when (error is SqlException or UnauthorizedAccessException or InvalidOperationException or OperationCanceledException)
                {
                    _logger.LogWarning("No se confirmó el recibo de despacho. Tipo {Type}; código {Code}.",
                        error.GetType().Name, (error as SqlException)?.Number);
                    TempData["ErrorMessage"] = "No fue posible recuperar el recibo de este despacho con la sesión vigente.";
                }
            }

            // Solo se pueden agregar pedidos mientras la ruta está en planificación.
            if (cabecera.Estado == "Planificada")
            {
                vm.PedidosDisponibles = await _logistics.GetAssignableOrdersAsync(null);
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrder(int rutaId, int pedidoId)
        {
            try
            {
                await _logistics.AddOrderToRouteAsync(rutaId, pedidoId);
                TempData["SuccessMessage"] = "Pedido agregado a la ruta.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar pedido a ruta.");
                TempData["ErrorMessage"] = "No fue posible agregar el pedido a la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveOrder(int rutaId, int pedidoId)
        {
            try
            {
                await _logistics.RemoveOrderFromRouteAsync(rutaId, pedidoId);
                TempData["SuccessMessage"] = "Pedido quitado de la ruta.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al quitar pedido de ruta.");
                TempData["ErrorMessage"] = "No fue posible quitar el pedido de la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dispatch(int rutaId, Guid dispatchId)
        {
            if (rutaId <= 0 || dispatchId == Guid.Empty) return BadRequest();
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                var result = await _logistics.DispatchRouteAsync(rutaId, dispatchId, usuarioId, usuarioNombre, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Ruta despachada. Los pedidos están En ruta.";
                // DispatchRouteAsync returns only after SQL COMMIT is confirmed.
                try
                {
                    var notices = await RouteDispatchNotificationBatch.SendOnceAsync(result.Items,
                        async (item, token) => (await _notifications.NotifyAsync(rutaId, result.DispatchId,
                            item.EventId, item.EventGuid, token)).State, HttpContext.RequestAborted);
                    if (notices.Unconfirmed > 0)
                        TempData["ErrorMessage"] = "El despacho quedó confirmado. Hay avisos cuyo resultado no se confirmó; consultá sus recibos antes de continuar.";
                    else if (notices.Eligible > 0)
                        TempData["SuccessMessage"] = "Ruta despachada. El servidor de correo aceptó sus avisos; esto no acredita la entrega al buzón.";
                }
                catch (Exception error)
                {
                    LogNotification(error);
                    TempData["ErrorMessage"] = "El despacho quedó confirmado. No se confirmó el estado de los avisos; consultá sus recibos.";
                }
            }
            catch (RouteDispatchCommitUnconfirmedException error)
            {
                _logger.LogWarning("Commit de despacho sin confirmar. Tipo {Type}.", error.InnerException?.GetType().Name);
                TempData["ErrorMessage"] = "No se confirmó el resultado del despacho. Consultá el recibo de esta operación antes de repetirla. No se inició ningún correo.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al despachar ruta.");
                TempData["ErrorMessage"] = "No fue posible despachar la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId, dispatchId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("route-dispatch-notification")]
        public Task<IActionResult> NotifyEmail(int rutaId, Guid dispatchId, long eventId, Guid eventGuid) =>
            DispatchNotice(rutaId, dispatchId, eventId, eventGuid, false);

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("route-dispatch-notification")]
        public Task<IActionResult> EmailReceipt(int rutaId, Guid dispatchId, long eventId, Guid eventGuid) =>
            DispatchNotice(rutaId, dispatchId, eventId, eventGuid, true);

        private async Task<IActionResult> DispatchNotice(int route, Guid dispatch, long item, Guid eventGuid, bool receiptOnly)
        {
            if (route <= 0 || dispatch == Guid.Empty || item <= 0 || eventGuid == Guid.Empty) return BadRequest();
            try
            {
                var result = receiptOnly
                    ? await _notifications.ReceiptAsync(route, dispatch, item, eventGuid, Guid.Empty, HttpContext.RequestAborted)
                    : await _notifications.NotifyAsync(route, dispatch, item, eventGuid, HttpContext.RequestAborted);
                return Json(new { ok = true, state = result.State, message = NotificationMessage(result.State) });
            }
            catch (UnauthorizedAccessException error)
            {
                LogNotification(error);
                return Unauthorized(new { ok = false, message = "La sesión no está vigente. Iniciá sesión nuevamente." });
            }
            catch (Exception error) when (error is RouteDispatchNotificationTransportException or HttpRequestException or OperationCanceledException)
            {
                LogNotification(error);
                return StatusCode(error is RouteDispatchNotificationTransportException { Status: 403 or 404 } transport ? transport.Status : 503,
                    new { ok = false, message = NotificationMessage("Unconfirmed") });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int rutaId, string? motivo)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                await _logistics.CancelRouteAsync(rutaId, motivo, usuarioId, usuarioNombre);
                TempData["SuccessMessage"] = "Ruta cancelada correctamente.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar ruta.");
                TempData["ErrorMessage"] = "No fue posible cancelar la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId });
        }

        // CU-251 E1 — Secuenciar automáticamente los puntos de entrega.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sequence(int rutaId)
        {
            try
            {
                var (puntos, sinCoord) = await _logistics.SequenceRouteAsync(rutaId);
                if (puntos == 0)
                {
                    TempData["ErrorMessage"] = "No hay pedidos pendientes para secuenciar en esta ruta.";
                }
                else
                {
                    if (sinCoord > 0)
                    {
                        TempData["ErrorMessage"] = $"{sinCoord} de {puntos} punto(s) no tienen coordenada y quedaron al final. Ubícalos en el mapa para que aparezcan en la app del chofer.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = $"Ruta secuenciada automáticamente ({puntos} punto(s)). Puede reordenar manualmente antes de despachar.";
                    }
                }
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al secuenciar ruta.");
                TempData["ErrorMessage"] = "No fue posible secuenciar la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId });
        }

        // CU-251 E3 — Guardar el orden manual y las coordenadas.
        // Las coordenadas se reciben como texto y se parsean con InvariantCulture:
        // los <input type="number"> siempre envían punto decimal, independientemente
        // de la cultura regional del servidor.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSequence(int rutaId, List<int> rutaPedidoId, List<int> secuencia, List<string?> lat, List<string?> lng)
        {
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                const System.Globalization.NumberStyles styles = System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowLeadingSign;

                var items = new List<object>();
                for (int i = 0; i < rutaPedidoId.Count; i++)
                {
                    decimal? latVal = i < lat.Count && decimal.TryParse(lat[i], styles, inv, out var la) ? la : (decimal?)null;
                    decimal? lngVal = i < lng.Count && decimal.TryParse(lng[i], styles, inv, out var lo) ? lo : (decimal?)null;
                    items.Add(new
                    {
                        rutaPedidoId = rutaPedidoId[i],
                        secuencia = i < secuencia.Count ? secuencia[i] : i + 1,
                        lat = latVal,
                        lng = lngVal
                    });
                }
                var json = System.Text.Json.JsonSerializer.Serialize(items);
                await _logistics.SaveSequenceAsync(rutaId, json);
                TempData["SuccessMessage"] = "Orden de entregas actualizado.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar secuencia de ruta.");
                TempData["ErrorMessage"] = "No fue posible guardar el orden de la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId });
        }

        // CU-253 — Recalcular la ruta en tiempo real ante un imprevisto.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recalculate(int rutaId, int? excluirRutaPedidoId, string? motivoFallo)
        {
            try
            {
                var puntos = await _logistics.RecalculateRouteAsync(rutaId, excluirRutaPedidoId, motivoFallo);
                if (puntos == 0)
                {
                    TempData["ErrorMessage"] = "No es posible recalcular: no quedan puntos por recalcular. Contacte al chofer manualmente.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Ruta recalculada. {puntos} punto(s) reordenados. El chofer verá la ruta actualizada.";
                }
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recalcular ruta.");
                TempData["ErrorMessage"] = "No fue posible recalcular la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId });
        }

        // CU-105 — Liquidar la ruta al final del día (reingresa inventario no entregado).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Liquidate(int rutaId)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                var (pedidos, unidades) = await _logistics.LiquidateRouteAsync(rutaId, usuarioId, usuarioNombre);
                TempData["SuccessMessage"] = pedidos > 0
                    ? $"Ruta liquidada. Se reingresaron {unidades} unidad(es) de {pedidos} pedido(s) no entregado(s)."
                    : "Ruta liquidada. No había mercadería pendiente por reingresar.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = "No fue posible completar la operación solicitada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al liquidar ruta.");
                TempData["ErrorMessage"] = "No fue posible liquidar la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId });
        }

        // CU-083 E3 — historial de evidencias por ruta.
        [HttpGet]
        [AdminAuthorize("Entregas", "ENTREGAS_EVIDENCIA_VER", allowAdministratorBypass: false)]
        public async Task<IActionResult> Evidences(int id)
        {
            var cabecera = await _logistics.GetRouteHeaderAsync(id);
            if (cabecera == null)
            {
                TempData["ErrorMessage"] = "No se encontró la ruta solicitada.";
                return RedirectToAction(nameof(Index));
            }

            var vm = await _logistics.GetRouteEvidencesAsync(id, cabecera);
            return View(vm);
        }

        // ── Helpers ─────────────────────────────────────────
        private async Task<RouteCreateViewModel> BuildCreateViewModelAsync()
        {
            var vm = new RouteCreateViewModel();
            await PopulateCreateOptionsAsync(vm);
            return vm;
        }

        private async Task PopulateCreateOptionsAsync(RouteCreateViewModel vm)
        {
            vm.PedidosDisponibles = await _logistics.GetAssignableOrdersAsync(null);
            vm.Choferes = await _logistics.GetAvailableDriversAsync();
            vm.Vehiculos = await _logistics.GetAvailableVehiclesAsync();
        }

        private void LogNotification(Exception error) => _logger.LogWarning(
            "Aviso de despacho sin confirmar. Tipo {Type}; estado {Status}.", error.GetType().Name,
            (error as RouteDispatchNotificationTransportException)?.Status);

        private static string NotificationMessage(string state) => state switch
        {
            "SmtpAccepted" => "El servidor de correo aceptó el aviso. Esto no acredita su entrega al buzón.",
            "Pending" => "El aviso está registrado y todavía no se inició su envío. Podés continuar el mismo intento.",
            "Sending" => "Hay un aviso en curso. Consultá su estado antes de realizar otra acción.",
            "Unknown" => "El resultado del aviso es desconocido. No se reenviará automáticamente para evitar duplicados.",
            "AlreadyRequested" => "Este evento ya tiene un aviso registrado. No se generó otro envío ni se recuperó un intento de otra sesión.",
            "None" => "Esta sesión no tiene un intento para el evento. Podés solicitar su primer aviso.",
            "NoRecipient" => "El despacho está confirmado, pero este evento no tiene un destinatario de correo válido acreditado.",
            "NoEvent" => "No se acreditó un evento de despacho para notificar.",
            _ => "El despacho y el correo son operaciones separadas. No se confirmó el aviso; consultá su recibo antes de continuar."
        };

        private async Task RegistrarAuditoriaAsync(string accion, string modulo, string descripcion)
        {
            await _adminDbService.CreateAuditLogAsync(
                HttpContext.Session.GetInt32("UserId"),
                HttpContext.Session.GetString("UserFullName"),
                HttpContext.Session.GetString("UserEmail"),
                HttpContext.Session.GetString("UserRole"),
                accion, modulo, descripcion,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }
    }
}
