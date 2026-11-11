using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-105 / CU-106 — Liquidación de cobros (efectivo + comprobantes del chofer) y de mercadería de una ruta.
    [AdminAuthorize("Finanzas", "LIQUIDACION_FINANCIERA")]
    public class FinanceController : Controller
    {
        private readonly FinanceDbService _finance;
        private readonly LogisticsDbService _logistics;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<FinanceController> _logger;

        public FinanceController(FinanceDbService finance, LogisticsDbService logistics, AdminDbService adminDbService, ILogger<FinanceController> logger)
        {
            _finance = finance;
            _logistics = logistics;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? estado)
        {
            var todas = await _finance.GetCashSettlementsAsync(null);
            var conLiquidacionVigente = todas.Where(l => !l.Anulada).Select(l => l.RutaId).ToHashSet();

            // BUG-10-04: solo las rutas completadas (finalizadas) se liquidan.
            var rutas = await _logistics.GetRoutesAsync(null, null);
            var model = new FinanceIndexViewModel
            {
                Estado = estado,
                RutasPendientes = rutas
                    .Where(r => r.Estado == "Completada" && !conLiquidacionVigente.Contains(r.RutaId))
                    .ToList(),
                Liquidaciones = string.IsNullOrWhiteSpace(estado) ? todas : todas.Where(l => l.Estado == estado).ToList(),
                Faltantes = await _finance.GetDriverShortagesAsync("Pendiente")
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Liquidate(int rutaId)
        {
            var prep = await _finance.PrepareCashSettlementAsync(rutaId);
            if (prep == null)
            {
                TempData["ErrorMessage"] = "No se encontró la ruta solicitada.";
                return RedirectToAction(nameof(Index));
            }
            if (prep.EstadoRuta != "Completada")
            {
                TempData["ErrorMessage"] = NotCompletedMessage(prep);
                return RedirectToAction(nameof(Index));
            }
            if (prep.YaLiquidadaFinanciera)
            {
                TempData["ErrorMessage"] = $"La ruta {prep.RutaCodigo} ya tiene liquidación financiera registrada. Para corregirla, anúlela con un motivo desde Liquidaciones registradas.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Prepare = prep;
            var model = new CashSettlementFormViewModel
            {
                RutaId = prep.RutaId,
                RutaCodigo = prep.RutaCodigo,
                EsperadoEfectivo = prep.EsperadoEfectivo,
                EsperadoOtros = prep.EsperadoOtros,
                MontoEfectivoRecibido = prep.EsperadoEfectivo,
                MontoComprobantes = 0m
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Liquidate(CashSettlementFormViewModel model)
        {
            var prep = await _finance.PrepareCashSettlementAsync(model.RutaId);
            if (prep == null)
            {
                TempData["ErrorMessage"] = "No se encontró la ruta solicitada.";
                return RedirectToAction(nameof(Index));
            }
            if (prep.EstadoRuta != "Completada")
            {
                TempData["ErrorMessage"] = NotCompletedMessage(prep);
                return RedirectToAction(nameof(Index));
            }

            // El esperado se vuelve a leer: no se confía en lo que envía el formulario.
            model.RutaCodigo = prep.RutaCodigo;
            model.EsperadoEfectivo = prep.EsperadoEfectivo;
            model.EsperadoOtros = prep.EsperadoOtros;
            ViewBag.Prepare = prep;

            // G2-38: el detalle de comprobantes debe sumar el total.
            var vouchersError = CashSettlementRules.ValidateVouchers(model.Comprobantes, model.MontoComprobantes);
            if (vouchersError != null) ModelState.AddModelError(string.Empty, vouchersError);

            if (!ModelState.IsValid) return View(model);

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Cajero";
            var comprobantesJson = FinanceDbService.BuildVouchersJson(model.Comprobantes);

            try
            {
                var result = await _finance.RegisterCashSettlementAsync(model, comprobantesJson, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Liquidar cobros de ruta", "Finanzas",
                    $"Liquidación financiera #{result.LiquidacionId} de la ruta {model.RutaCodigo}: {result.Estado} (diferencia {Colones(result.Diferencia)})."
                    + (result.FaltanteChofer > 0 ? $" Faltante de {Colones(result.FaltanteChofer)} a cargo del chofer {result.ChoferNombre}." : string.Empty));
                TempData["SuccessMessage"] = CashSettlementRules.SuccessMessage(result);
                TempData["ReceiptLiquidacionId"] = result.LiquidacionId;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar la liquidación financiera de la ruta {RutaId}.", model.RutaId);
                ModelState.AddModelError(string.Empty, BusinessErrors.MessageOr(ex, "No fue posible registrar la liquidación."));
                return View(model);
            }
        }

        // G2-37: corregir una liquidación ya registrada.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSettlement(int liquidacionId, string? motivo)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Cajero";
            try
            {
                var rutaCodigo = await _finance.CancelCashSettlementAsync(liquidacionId, motivo ?? string.Empty, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Anular liquidación de cobros", "Finanzas",
                    $"Se anuló la liquidación financiera #{liquidacionId} de la ruta {rutaCodigo}. Motivo: {motivo?.Trim()}.");
                TempData["SuccessMessage"] = $"Liquidación anulada. La ruta {rutaCodigo} puede liquidarse de nuevo.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo anular la liquidación {LiquidacionId}.", liquidacionId);
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible anular la liquidación.");
            }
            return RedirectToAction(nameof(Index));
        }

        // BUG-10-03: recibo en PDF de una liquidación registrada.
        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            try
            {
                var recibo = await _finance.GetCashSettlementReceiptAsync(id);
                if (recibo == null)
                {
                    TempData["ErrorMessage"] = "No se encontró la liquidación solicitada.";
                    return RedirectToAction(nameof(Index));
                }
                var bytes = CashSettlementReceiptPdf.Build(recibo);
                Response.Headers.ContentDisposition = $"inline; filename=\"{CashSettlementReceiptPdf.FileName(recibo)}\"";
                return File(bytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo generar el recibo de la liquidación {LiquidacionId}.", id);
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(ex, "No fue posible generar el recibo.");
                return RedirectToAction(nameof(Index));
            }
        }

        // ── BUG-10-07 · CU-105 Liquidación de mercadería de una ruta completada ──
        [HttpGet]
        public async Task<IActionResult> Merchandise()
        {
            try
            {
                return View(await _finance.GetMerchandiseRoutesAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo cargar la liquidación de mercadería.");
                TempData["ErrorMessage"] = "No fue posible cargar las rutas pendientes de liquidar mercadería.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> LiquidateMerchandise(int rutaId)
        {
            var prep = await _finance.PrepareMerchandiseAsync(rutaId);
            var blocked = MerchandiseBlockedMessage(prep);
            if (blocked != null)
            {
                TempData["ErrorMessage"] = blocked;
                return RedirectToAction(nameof(Merchandise));
            }

            ViewBag.Prepare = prep;
            return View(new RouteMerchandiseFormViewModel
            {
                RutaId = prep!.RutaId,
                // Se precarga lo despachado: quien liquida solo corrige lo que no llegó.
                Lineas = prep.Lineas.Select(l => new RouteMerchandiseLine
                {
                    PedidoId = l.PedidoId, ProductoId = l.ProductoId, ProductoNombre = l.ProductoNombre,
                    Cantidad = l.Cantidad, Contado = l.Cantidad
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LiquidateMerchandise(RouteMerchandiseFormViewModel model)
        {
            var prep = await _finance.PrepareMerchandiseAsync(model.RutaId);
            var blocked = MerchandiseBlockedMessage(prep);
            if (blocked != null)
            {
                TempData["ErrorMessage"] = blocked;
                return RedirectToAction(nameof(Merchandise));
            }

            // Las líneas (pedido, producto, cantidad despachada) salen de la base; del formulario solo se toma lo contado.
            var contado = (model.Lineas ?? new()).GroupBy(l => (l.PedidoId, l.ProductoId))
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Contado));
            model.Lineas = prep!.Lineas.Select(l => new RouteMerchandiseLine
            {
                PedidoId = l.PedidoId, ProductoId = l.ProductoId, ProductoNombre = l.ProductoNombre, Cantidad = l.Cantidad,
                Contado = contado.TryGetValue((l.PedidoId, l.ProductoId), out var c) ? c : 0
            }).ToList();
            ViewBag.Prepare = prep;

            if (model.Lineas.Any(l => l.Contado < 0))
                ModelState.AddModelError(string.Empty, "Las cantidades contadas no pueden ser negativas.");
            if (!ModelState.IsValid) return View(model);

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Usuario";
            try
            {
                var result = await _finance.RegisterMerchandiseAsync(model, usuarioId, usuarioNombre);
                await RegistrarAuditoriaAsync("Liquidar mercadería de ruta", "Finanzas",
                    $"Liquidación de mercadería #{result.MercaderiaId} de la ruta {prep.RutaCodigo}: {result.Unidades} unidades de {result.Lineas} líneas regresaron al inventario.");
                TempData["SuccessMessage"] = $"Mercadería liquidada: {result.Unidades} unidades regresaron al inventario (ruta {prep.RutaCodigo}).";
                return RedirectToAction(nameof(Merchandise));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo liquidar la mercadería de la ruta {RutaId}.", model.RutaId);
                ModelState.AddModelError(string.Empty, BusinessErrors.MessageOr(ex, "No fue posible liquidar la mercadería."));
                return View(model);
            }
        }

        private static string? MerchandiseBlockedMessage(RouteMerchandisePrepareViewModel? prep)
        {
            if (prep == null) return "No se encontró la ruta solicitada.";
            if (prep.EstadoRuta != "Completada")
                return $"Solo se puede liquidar la mercadería de una ruta completada. La ruta {prep.RutaCodigo} está {prep.EstadoRuta.ToLowerInvariant()}.";
            if (prep.Liquidada || prep.YaRegistrada)
                return $"La mercadería de la ruta {prep.RutaCodigo} ya fue liquidada.";
            if (prep.Lineas.Count == 0)
                return $"La ruta {prep.RutaCodigo} no tiene pedidos sin entregar: no hay mercadería que devolver.";
            return null;
        }

        private static string NotCompletedMessage(CashSettlementPrepareViewModel prep) =>
            $"Solo se puede liquidar una ruta completada. La ruta {prep.RutaCodigo} está {prep.EstadoRuta.ToLowerInvariant()}"
            + (prep.EstadoRuta == "Despachada" ? " y todavía no finaliza." : ".");

        private static string Colones(decimal monto) => $"₡{monto:N2}";

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
