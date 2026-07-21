using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    // CU-082 (estado de entrega + offline + notificación) y CU-083 E1/E2 (evidencia).
    [SessionAuthorize("Chofer", "Administrador")]
    public class DriverDeliveriesController : Controller
    {
        private const long MaxEvidenceBytes = 5 * 1024 * 1024;
        private static readonly IReadOnlyDictionary<string, string[]> PermittedImageTypes =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = new[] { "image/jpeg" },
                [".jpeg"] = new[] { "image/jpeg" },
                [".png"] = new[] { "image/png" },
                [".webp"] = new[] { "image/webp" }
            };

        private readonly LogisticsDbService _logistics;
        private readonly AdminDbService _adminDbService;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DriverDeliveriesController> _logger;

        public DriverDeliveriesController(
            LogisticsDbService logistics,
            AdminDbService adminDbService,
            EmailService emailService,
            IWebHostEnvironment environment,
            ILogger<DriverDeliveriesController> logger)
        {
            _logistics = logistics;
            _adminDbService = adminDbService;
            _emailService = emailService;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var choferId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var rutas = await _logistics.GetDriverRoutesAsync(choferId);
            return View(rutas);
        }

        [HttpGet]
        public async Task<IActionResult> Route(int id)
        {
            var choferId = HttpContext.Session.GetInt32("UserId") ?? 0;

            try
            {
                var vm = await _logistics.GetDriverRouteAsync(id, choferId);
                if (vm == null)
                {
                    TempData["ErrorMessage"] = "No se encontró la ruta solicitada.";
                    return RedirectToAction(nameof(Index));
                }
                return View(vm);
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // CU-082 E1/E2/E3 — endpoint JSON usado en línea y por la cola offline.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int rutaPedidoId, string nuevoEstado, string? syncGuid, string? motivoFallo)
        {
            var choferId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var choferNombre = HttpContext.Session.GetString("UserFullName") ?? "Chofer";

            if (!Guid.TryParse(syncGuid, out var guid))
                guid = Guid.NewGuid();

            try
            {
                var result = await _logistics.UpdateDeliveryStatusAsync(
                    rutaPedidoId, nuevoEstado, guid, motivoFallo, choferId, choferNombre);

                if (result == null)
                    return Json(new { ok = false, message = "No se pudo actualizar la entrega." });

                if (!result.Duplicado)
                {
                    await RegistrarAuditoriaAsync("Actualizar entrega", "Entregas",
                        $"Pedido #{result.PedidoId}: estado de entrega {result.EstadoEntrega}.");

                    // CU-082 E3 — notificación al cliente (mejor esfuerzo).
                    if (result.Notificar)
                        NotificarClienteSeguro(result.ClienteCorreo, result.ClienteNombre, result.PedidoId,
                            result.EstadoEntrega == "Entregado" ? "Entregado" : "En ruta");
                }

                return Json(new
                {
                    ok = true,
                    estado = result.EstadoEntrega,
                    pedidoId = result.PedidoId,
                    rutaCompletada = result.RutaCompletada,
                    duplicado = result.Duplicado
                });
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                return Json(new { ok = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar estado de entrega.");
                return Json(new { ok = false, message = "No fue posible actualizar la entrega." });
            }
        }

        // CU-083 E1 — registrar evidencia (foto subida o firma en canvas).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> RegisterEvidence(
            int pedidoId, int rutaId, string tipo, string? observaciones,
            IFormFile? archivo, string? firmaBase64)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Chofer";

            try
            {
                string? archivoUrl = string.Equals(tipo, "Firma", StringComparison.OrdinalIgnoreCase)
                    ? await SaveSignatureAsync(firmaBase64, pedidoId)
                    : await SavePhotoAsync(archivo, pedidoId);

                if (string.IsNullOrWhiteSpace(archivoUrl))
                {
                    TempData["ErrorMessage"] = "Debe adjuntar una foto o firma válida.";
                    return RedirectToAction(nameof(Route), new { id = rutaId });
                }

                var tipoNormalizado = string.Equals(tipo, "Firma", StringComparison.OrdinalIgnoreCase) ? "Firma" : "Foto";

                await _logistics.RegisterEvidenceAsync(
                    pedidoId, rutaId, tipoNormalizado, archivoUrl, observaciones, usuarioId, usuarioNombre);

                await RegistrarAuditoriaAsync("Registrar evidencia", "Entregas",
                    $"Evidencia ({tipoNormalizado}) registrada para el pedido #{pedidoId}.");

                TempData["SuccessMessage"] = "Evidencia registrada correctamente.";
            }
            catch (EvidenceValidationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (SqlException ex) when (ex.Number >= 50000)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar evidencia de entrega.");
                TempData["ErrorMessage"] = "No fue posible registrar la evidencia.";
            }

            return RedirectToAction(nameof(Route), new { id = rutaId });
        }

        // ── Guardado de archivos ────────────────────────────
        private async Task<string?> SavePhotoAsync(IFormFile? archivo, int pedidoId)
        {
            if (archivo == null || archivo.Length == 0) return null;

            if (archivo.Length > MaxEvidenceBytes)
                throw new EvidenceValidationException("La imagen supera el tamaño máximo permitido de 5 MB.");

            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !PermittedImageTypes.TryGetValue(extension, out var permitidos))
                throw new EvidenceValidationException("La imagen debe estar en formato JPG, JPEG, PNG o WEBP.");

            if (string.IsNullOrWhiteSpace(archivo.ContentType) || !permitidos.Contains(archivo.ContentType, StringComparer.OrdinalIgnoreCase))
                throw new EvidenceValidationException("El tipo de archivo no coincide con una imagen válida.");

            var folder = EnsureEvidenceFolder();
            var fileName = $"pedido{pedidoId}-{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return $"/uploads/evidencias/{fileName}";
        }

        private async Task<string?> SaveSignatureAsync(string? firmaBase64, int pedidoId)
        {
            if (string.IsNullOrWhiteSpace(firmaBase64)) return null;

            var comma = firmaBase64.IndexOf(',');
            var base64 = comma >= 0 ? firmaBase64[(comma + 1)..] : firmaBase64;

            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64); }
            catch { throw new EvidenceValidationException("La firma capturada no es válida."); }

            if (bytes.Length == 0) return null;
            if (bytes.Length > MaxEvidenceBytes)
                throw new EvidenceValidationException("La firma supera el tamaño máximo permitido.");

            var folder = EnsureEvidenceFolder();
            var fileName = $"firma-pedido{pedidoId}-{Guid.NewGuid():N}.png";
            var fullPath = Path.Combine(folder, fileName);
            await System.IO.File.WriteAllBytesAsync(fullPath, bytes);

            return $"/uploads/evidencias/{fileName}";
        }

        private string EnsureEvidenceFolder()
        {
            var root = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                : _environment.WebRootPath;
            var folder = Path.Combine(root, "uploads", "evidencias");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private void NotificarClienteSeguro(string correo, string nombre, int pedidoId, string estado)
        {
            if (string.IsNullOrWhiteSpace(correo)) return;
            try
            {
                var asunto = $"Actualización de su pedido #{pedidoId} - {estado}";
                var cuerpo =
                    $"<p>Hola {System.Net.WebUtility.HtmlEncode(nombre)},</p>" +
                    $"<p>Su pedido <strong>#{pedidoId}</strong> ahora está <strong>{System.Net.WebUtility.HtmlEncode(estado)}</strong>.</p>" +
                    "<p>Gracias por su preferencia.<br/>Licorera La Bodega</p>";
                _emailService.SendEmail(correo, asunto, cuerpo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar la notificación al cliente del pedido #{PedidoId}.", pedidoId);
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

        private sealed class EvidenceValidationException : Exception
        {
            public EvidenceValidationException(string message) : base(message) { }
        }
    }
}
