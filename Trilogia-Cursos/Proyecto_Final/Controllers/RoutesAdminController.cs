using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-081 (rutas de entrega) + CU-083 E3 (consulta de evidencias por ruta).
    [AdminAuthorize("Rutas", "RUTAS_GESTIONAR")]
    public class RoutesAdminController : Controller
    {
        private readonly LogisticsDbService _logistics;
        private readonly AdminDbService _adminDbService;
        private readonly EmailService _emailService;
        private readonly ILogger<RoutesAdminController> _logger;

        public RoutesAdminController(
            LogisticsDbService logistics,
            AdminDbService adminDbService,
            EmailService emailService,
            ILogger<RoutesAdminController> logger)
        {
            _logistics = logistics;
            _adminDbService = adminDbService;
            _emailService = emailService;
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
                    model.PedidosSeleccionados, usuarioId, usuarioNombre, model.Prioridades);

                await RegistrarAuditoriaAsync(
                    "Crear ruta", "Rutas",
                    $"Se creó la ruta {codigo} (zona {model.Zona}) con {model.PedidosSeleccionados.Count} pedido(s).");

                TempData["SuccessMessage"] = $"Ruta {codigo} creada correctamente.";
                return RedirectToAction(nameof(Detail), new { id = rutaId });
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                ModelState.AddModelError(string.Empty, BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada."));
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
        public async Task<IActionResult> Detail(int id)
        {
            var cabecera = await _logistics.GetRouteHeaderAsync(id);
            if (cabecera == null)
            {
                TempData["ErrorMessage"] = "No se encontró la ruta solicitada.";
                return RedirectToAction(nameof(Index));
            }

            var pedidos = await _logistics.GetRouteOrdersAsync(id);
            var vm = new RouteDetailViewModel
            {
                Cabecera = cabecera,
                Pedidos = pedidos,
                Estimacion = RouteEstimator.Estimate(pedidos)
            };

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
                await RegistrarAuditoriaAsync("Modificar ruta", "Rutas", $"Se agregó el pedido #{pedidoId} a la ruta #{rutaId}.");
                TempData["SuccessMessage"] = "Pedido agregado a la ruta.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
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
                await RegistrarAuditoriaAsync("Modificar ruta", "Rutas", $"Se quitó el pedido #{pedidoId} de la ruta #{rutaId}.");
                TempData["SuccessMessage"] = "Pedido quitado de la ruta.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
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
        public async Task<IActionResult> Dispatch(int rutaId)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                var destinatarios = await _logistics.DispatchRouteAsync(rutaId, usuarioId, usuarioNombre);
                // Los pedidos toman el estado de su entrega (En ruta), como cuando el chofer usa la app.
                var pedidosSincronizados = await SyncOrderStatusAsync(rutaId);

                await RegistrarAuditoriaAsync("Despachar ruta", "Rutas",
                    $"Se despachó la ruta #{rutaId}. {destinatarios.Count} pedido(s) pasaron a En ruta.");

                // CU-082 E3: notificación al cliente (mejor esfuerzo, no bloquea el despacho).
                foreach (var (pedidoId, nombre, correo) in destinatarios)
                {
                    NotificarClienteSeguro(correo, nombre, pedidoId, "En ruta");
                }

                if (pedidosSincronizados)
                {
                    TempData["SuccessMessage"] = "Ruta despachada. Los pedidos están En ruta.";
                }
                else
                {
                    TempData["ErrorMessage"] = "La ruta se despachó, pero no se pudo actualizar el estado de sus pedidos a En ruta. Avise a soporte.";
                }
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al despachar ruta.");
                TempData["ErrorMessage"] = "No fue posible despachar la ruta.";
            }
            return RedirectToAction(nameof(Detail), new { id = rutaId });
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
                await RegistrarAuditoriaAsync("Cancelar ruta", "Rutas", $"Se canceló la ruta #{rutaId}. Motivo: {motivo}.");
                TempData["SuccessMessage"] = "Ruta cancelada correctamente.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
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
                // BUG-25-01: sp_Rutas_Secuenciar ya no procesa una ruta con paradas sin coordenada:
                // la rechaza (54012) diciendo cuántas faltan y no cambia el orden. Ese texto es el
                // que se muestra; hay que ubicar los puntos en el mapa y volver a secuenciar.
                var (puntos, _) = await _logistics.SequenceRouteAsync(rutaId);
                if (puntos == 0)
                {
                    TempData["ErrorMessage"] = "No hay pedidos pendientes para secuenciar en esta ruta.";
                }
                else
                {
                    await RegistrarAuditoriaAsync("Secuenciar ruta", "Rutas", $"Se secuenciaron automáticamente {puntos} punto(s) de la ruta #{rutaId}.");
                    TempData["SuccessMessage"] =
                        JoinSentences($"Ruta secuenciada automáticamente ({puntos} punto(s)).", await EstimateTextAsync(rutaId), "Puede reordenar manualmente antes de despachar.");
                }
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
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
        public async Task<IActionResult> SaveSequence(int rutaId, List<int> rutaPedidoId, List<int> secuencia, List<string?> lat, List<string?> lng, List<int>? prioridad = null)
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
                        lng = lngVal,
                        prioridad = prioridad != null && i < prioridad.Count ? prioridad[i] : (int?)null
                    });
                }
                var json = System.Text.Json.JsonSerializer.Serialize(items);
                await _logistics.SaveSequenceAsync(rutaId, json);
                await RegistrarAuditoriaAsync("Reordenar ruta", "Rutas", $"Se guardó el orden manual de la ruta #{rutaId}.");
                TempData["SuccessMessage"] = "Orden de entregas actualizado.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
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
                // Excluir un punto lo marca Fallido: el pedido también pasa a No entregado.
                if (excluirRutaPedidoId.HasValue) await SyncOrderStatusAsync(rutaId);
                if (puntos == 0)
                {
                    TempData["ErrorMessage"] = "No es posible recalcular: no quedan puntos por recalcular. Contacte al chofer manualmente.";
                }
                else
                {
                    await RegistrarAuditoriaAsync("Recalcular ruta", "Rutas", $"Se recalculó la ruta #{rutaId} ({puntos} punto(s) reordenados).");
                    TempData["SuccessMessage"] = JoinSentences($"Ruta recalculada. {puntos} punto(s) reordenados.", await EstimateTextAsync(rutaId), "El chofer verá la ruta actualizada.");
                }
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
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
                await RegistrarAuditoriaAsync("Liquidar ruta", "Rutas",
                    $"Se liquidó la ruta #{rutaId}. {pedidos} pedido(s) y {unidades} unidad(es) reingresadas al inventario.");
                TempData["SuccessMessage"] = pedidos > 0
                    ? $"Ruta liquidada. Se reingresaron {unidades} unidad(es) de {pedidos} pedido(s) no entregado(s)."
                    : "Ruta liquidada. No había mercadería pendiente por reingresar.";
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                _logger.LogWarning(ex, "La base de datos rechazó una operación de negocio.");
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible completar la operación solicitada.");
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
        [AdminAuthorize("Entregas", "ENTREGAS_EVIDENCIA_VER")]
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

        // Lleva el estado de las entregas de la ruta a sus pedidos (sp_Pedidos_SincronizarEstadoEntrega, 0220).
        // La operación principal ya se hizo: si esto falla se registra y se avisa, no se deshace nada.
        private async Task<bool> SyncOrderStatusAsync(int rutaId)
        {
            try
            {
                await _logistics.SyncOrderStatusFromDeliveryAsync(rutaId, null);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo sincronizar el estado de los pedidos de la ruta #{RutaId}.", rutaId);
                return false;
            }
        }

        private static string JoinSentences(params string[] frases) =>
            string.Join(" ", frases.Where(frase => !string.IsNullOrWhiteSpace(frase)));

        // BUG-25-02: «Tiempo total estimado: 1 h 12 min (38,4 km).» para el mensaje de secuenciar y recalcular.
        // Es informativo: si falla la lectura no debe impedir el aviso de que la ruta ya quedó ordenada.
        private async Task<string> EstimateTextAsync(int rutaId)
        {
            try
            {
                var estimacion = RouteEstimator.Estimate(await _logistics.GetRouteOrdersAsync(rutaId));
                if (!estimacion.TieneEstimacion) return string.Empty;
                var texto = $"Tiempo total estimado: {estimacion.TextoTotal} ({estimacion.TextoDistancia}).";
                return estimacion.ParadasSinUbicacion > 0
                    ? $"{texto} No incluye {estimacion.ParadasSinUbicacion} parada(s) sin ubicación."
                    : texto;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo calcular el tiempo estimado de la ruta #{RutaId}.", rutaId);
                return string.Empty;
            }
        }

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

        private void NotificarClienteSeguro(string correo, string nombre, int pedidoId, string estado)
        {
            if (string.IsNullOrWhiteSpace(correo)) return;

            try
            {
                var asunto = $"Actualización de su pedido #{pedidoId} - {estado}";
                var cuerpo =
                    $"<p>Hola {System.Net.WebUtility.HtmlEncode(nombre)},</p>" +
                    $"<p>Le informamos que su pedido <strong>#{pedidoId}</strong> ahora está en estado " +
                    $"<strong>{System.Net.WebUtility.HtmlEncode(estado)}</strong>.</p>" +
                    "<p>Gracias por su preferencia.<br/>Licorera La Bodega</p>";
                _emailService.SendEmail(correo, asunto, cuerpo);
            }
            catch (Exception ex)
            {
                // Mejor esfuerzo: un fallo de correo no debe afectar la operación.
                _logger.LogWarning(ex, "No se pudo enviar la notificación de entrega al cliente del pedido #{PedidoId}.", pedidoId);
            }
        }

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
