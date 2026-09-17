using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;
using QRCoder;

namespace Proyecto_Final.Controllers
{
    /// <summary>
    /// Descarga de la aplicación móvil.
    ///
    /// La página es, ante todo, una pantalla de instalación: código QR, botón de
    /// descarga e instrucciones. Publicar una versión es trabajo del equipo de
    /// desarrollo y está detrás de un permiso, apartado de lo que ve un chofer.
    ///
    /// Requiere sesión a propósito. Un teléfono nuevo escanea el QR desde la
    /// pantalla de un compañero ya autenticado, así que el flujo sigue siendo de
    /// dos segundos; en cambio, un APK interno colgado en una dirección pública
    /// le regala a cualquiera el material para estudiar los endpoints.
    /// </summary>
    [SessionAuthorize]
    public sealed class MobileAppController : Controller
    {
        private readonly IMobileAppReleaseService _releases;
        private readonly IMobileAppPackageStorage _storage;
        private readonly IRolePermissionService _permissions;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<MobileAppController> _logger;

        public MobileAppController(
            IMobileAppReleaseService releases,
            IMobileAppPackageStorage storage,
            IRolePermissionService permissions,
            AdminDbService adminDbService,
            ILogger<MobileAppController> logger)
        {
            _releases = releases;
            _storage = storage;
            _permissions = permissions;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            try
            {
                var canPublish = await CanPublishAsync();
                var vigente = await _releases.GetCurrentAsync(cancellationToken);

                var model = new MobileAppPageViewModel
                {
                    Vigente = vigente,
                    PuedePublicar = canPublish,
                    UrlInstalacion = BuildInstallUrl(),
                    // Si el archivo se subió al sistema pero ya no está en disco,
                    // conviene decirlo en vez de ofrecer una descarga rota.
                    ArchivoFaltante = vigente is not null
                                      && vigente.EstaEnElSistema
                                      && !_storage.Exists(vigente.ArchivoAlmacenado)
                };

                if (canPublish) model.Historial = await _releases.GetAllAsync(cancellationToken);

                return View(model);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudo cargar la página de descarga de la aplicación móvil.");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "El servicio no está disponible en este momento.");
            }
        }

        /// <summary>
        /// Genera el código QR en el servidor.
        ///
        /// No se usa una biblioteca de un CDN: la política de contenido del sitio
        /// restringe orígenes, y pedirle el QR a un tercero significa enviarle la
        /// dirección de distribución interna.
        /// </summary>
        [HttpGet]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
        public IActionResult Qr()
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(BuildInstallUrl(), QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(10);

            return File(png, "image/png");
        }

        /// <summary>
        /// Entrega el archivo de la versión vigente y deja constancia de quién
        /// lo descargó.
        ///
        /// Si el archivo vive en el sistema, se sirve desde aquí. Si la versión
        /// apunta a una dirección externa, se redirige. En ambos casos la
        /// dirección real del archivo nunca aparece en la página, así que se
        /// puede cambiar el alojamiento sin redesplegar nada.
        /// </summary>
        [HttpGet]
        [EnableRateLimiting("sensitive-read")]
        public async Task<IActionResult> Download(CancellationToken cancellationToken)
        {
            MobileAppReleaseViewModel? release;
            try
            {
                release = await _releases.GetCurrentAsync(cancellationToken);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudo resolver la versión vigente para descarga.");
                TempData["ErrorMessage"] = "No fue posible obtener la aplicación en este momento.";
                return RedirectToAction(nameof(Index));
            }

            if (release is null)
            {
                TempData["ErrorMessage"] = "Todavía no hay una versión publicada de la aplicación.";
                return RedirectToAction(nameof(Index));
            }

            if (release.EstaEnElSistema)
            {
                var stream = await _storage.OpenReadAsync(release.ArchivoAlmacenado, cancellationToken);
                if (stream is null)
                {
                    _logger.LogError(
                        "La versión {VersionId} apunta a un archivo que ya no está en el almacén.",
                        release.VersionId);
                    TempData["ErrorMessage"] =
                        "El archivo de esta versión no está disponible. Avisale al equipo de desarrollo.";
                    return RedirectToAction(nameof(Index));
                }

                await RegisterAuditAsync(
                    "Descargar aplicación móvil",
                    "Movil",
                    $"Descarga de la versión {release.VersionNombre} (compilación {release.BuildNumero}).");

                var nombre = string.IsNullOrWhiteSpace(release.ArchivoNombre)
                    ? $"LaBodega-{release.VersionNombre}.apk"
                    : release.ArchivoNombre;

                // El tipo es el que Android espera para ofrecer la instalación.
                return File(stream, "application/vnd.android.package-archive", nombre);
            }

            // Cinturón y tirantes: la restricción de la tabla ya lo exige, pero un
            // redirect es exactamente donde no se quiere un descuido.
            if (!Uri.TryCreate(release.UrlDescarga, UriKind.Absolute, out var target) ||
                !string.Equals(target.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "La versión {VersionId} no tiene archivo ni una dirección HTTPS válida.",
                    release.VersionId);
                TempData["ErrorMessage"] = "La versión publicada no tiene una descarga válida.";
                return RedirectToAction(nameof(Index));
            }

            await RegisterAuditAsync(
                "Descargar aplicación móvil",
                "Movil",
                $"Descarga de la versión {release.VersionNombre} (compilación {release.BuildNumero}).");

            return Redirect(target.AbsoluteUri);
        }

        /// <summary>
        /// Publica una versión nueva subiendo el archivo.
        ///
        /// La huella y el tamaño los calcula el sistema sobre el archivo
        /// recibido: son datos que no se piden ni se aceptan del formulario,
        /// porque escribir 64 caracteres a mano garantiza equivocarse y porque
        /// una huella que no corresponde al archivo no sirve para nada.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Movil", "MOVIL_APP_PUBLICAR")]
        [EnableRateLimiting("private-file-upload")]
        [RequestSizeLimit(160 * 1024 * 1024)]
        public async Task<IActionResult> Publish(
            MobileAppPublishViewModel model,
            IFormFile? archivo,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Revisá los datos de la versión.";
                return RedirectToAction(nameof(Index));
            }

            if (model.MinBuildSoportado > model.BuildNumero)
            {
                TempData["ErrorMessage"] = "La versión mínima no puede ser mayor que la que se publica.";
                return RedirectToAction(nameof(Index));
            }

            StoredPackage? paquete = null;
            var confirmado = false;

            try
            {
                paquete = await _storage.SaveAsync(archivo, cancellationToken);

                var versionId = await _releases.PublishAsync(
                    new MobileAppPublishRequest
                    {
                        VersionNombre = model.VersionNombre,
                        BuildNumero = model.BuildNumero,
                        MinBuildSoportado = model.MinBuildSoportado,
                        Sha256 = paquete.Sha256,
                        ArchivoAlmacenado = paquete.StorageKey,
                        ArchivoNombre = paquete.FileName,
                        TamanoBytes = paquete.SizeBytes,
                        Notas = model.Notas,
                        MensajeObligatorio = model.MensajeObligatorio
                    },
                    HttpContext.Session.GetInt32("UserId") ?? 0,
                    HttpContext.Session.GetString("UserFullName") ?? "Usuario",
                    cancellationToken);

                confirmado = true;

                await RegisterAuditAsync(
                    "Publicar versión móvil",
                    "Movil",
                    $"Versión {model.VersionNombre} (compilación {model.BuildNumero}) publicada. ID: {versionId}.");

                TempData["SuccessMessage"] =
                    $"Versión {model.VersionNombre} publicada. Ya se puede instalar escaneando el código.";
            }
            catch (PackageValidationException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(exception, "Publicación de versión móvil rechazada.");
                TempData["ErrorMessage"] = exception.Number switch
                {
                    55110 => "No se pudo calcular la huella del archivo.",
                    55111 => "Hay que subir el archivo de la aplicación.",
                    55113 => "La versión mínima no puede ser mayor que la que se publica.",
                    55114 => "El número de compilación debe ser mayor que el de la última versión publicada.",
                    _ => "No fue posible publicar la versión."
                };
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error al publicar una versión de la aplicación móvil.");
                TempData["ErrorMessage"] = "No fue posible publicar la versión.";
            }
            finally
            {
                // Si el registro no llegó a crearse, el archivo quedaría huérfano
                // ocupando espacio sin que nada lo referencie.
                if (paquete is not null && !confirmado)
                {
                    try { await _storage.DeleteAsync(paquete.StorageKey); }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "No se pudo limpiar un paquete móvil sin registrar.");
                    }
                }
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Movil", "MOVIL_APP_PUBLICAR")]
        public async Task<IActionResult> Unpublish(int versionId, CancellationToken cancellationToken)
        {
            try
            {
                if (await _releases.UnpublishAsync(versionId, cancellationToken))
                {
                    await RegisterAuditAsync(
                        "Retirar versión móvil",
                        "Movil",
                        $"Versión ID {versionId} retirada de circulación.");
                    TempData["SuccessMessage"] = "La versión se retiró de circulación.";
                }
                else
                {
                    TempData["ErrorMessage"] = "No se encontró la versión indicada.";
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error al retirar la versión móvil {VersionId}.", versionId);
                TempData["ErrorMessage"] = "No fue posible retirar la versión.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CanPublishAsync()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase)) return true;

            try
            {
                return await _permissions.HasCodePermissionAsync(role ?? string.Empty, "MOVIL_APP_PUBLICAR");
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No se pudo verificar el permiso MOVIL_APP_PUBLICAR.");
                return false;
            }
        }

        private string BuildInstallUrl() =>
            Url.Action(nameof(Download), "MobileApp", null, Request.Scheme)
            ?? $"{Request.Scheme}://{Request.Host}/MobileApp/Download";

        private Task RegisterAuditAsync(string action, string module, string description) =>
            _adminDbService.CreateAuditLogAsync(
                HttpContext.Session.GetInt32("UserId"),
                HttpContext.Session.GetString("UserFullName"),
                HttpContext.Session.GetString("UserEmail"),
                HttpContext.Session.GetString("UserRole"),
                action,
                module,
                description,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
    }
}
