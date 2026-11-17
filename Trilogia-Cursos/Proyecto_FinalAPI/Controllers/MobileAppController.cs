using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Compuerta de versión mínima.
    ///
    /// Es el único endpoint móvil sin autenticación, y tiene que serlo: una
    /// aplicación demasiado vieja para seguir usándose debe poder enterarse
    /// <b>antes</b> de intentar iniciar sesión, y el contrato de login podría
    /// ser justo lo que cambió.
    ///
    /// No expone nada sensible: número de versión, número de compilación, el
    /// enlace público de descarga y el mensaje de actualización obligatoria.
    /// </summary>
    [ApiController]
    [Route("api/mobile/v1/app")]
    [EnableRateLimiting("mobile-read")]
    public sealed class MobileAppController : ControllerBase
    {
        private readonly IAppReleaseDbService _releases;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MobileAppController> _logger;

        public MobileAppController(
            IAppReleaseDbService releases,
            IConfiguration configuration,
            ILogger<MobileAppController> logger)
        {
            _releases = releases;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("version")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Version(CancellationToken cancellationToken)
        {
            try
            {
                var release = await _releases.GetCurrentAsync(cancellationToken);

                if (release is null)
                {
                    // Todavía no se publicó ninguna versión. Se responde con la
                    // compuerta abierta en vez de un error: bloquear a todo el
                    // mundo porque falta un registro administrativo sería peor
                    // que el problema que la compuerta resuelve.
                    return Ok(new MobileAppVersionResponse
                    {
                        LatestBuild = 0,
                        LatestVersion = string.Empty,
                        MinSupportedBuild = 0,
                        DownloadUrl = string.Empty
                    });
                }

                return Ok(new MobileAppVersionResponse
                {
                    LatestBuild = release.BuildNumero,
                    LatestVersion = release.VersionNombre,
                    MinSupportedBuild = release.MinBuildSoportado,
                    DownloadUrl = ResolveDownloadUrl(release.UrlDescarga),
                    MandatoryMessage = release.MensajeObligatorio
                });
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudo consultar la versión vigente de la aplicación móvil.");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new MobileError("servicio_no_disponible", "No fue posible verificar la versión. Intente de nuevo."));
            }
        }

        /// <summary>
        /// Enlace que la aplicación abre para actualizarse.
        ///
        /// Si la versión apunta a una dirección externa, se usa esa. Si el APK
        /// se subió al propio sistema (UrlDescarga vacía), el enlace es la
        /// descarga del sitio web, que pide sesión y deja constancia. Sin este
        /// respaldo la pantalla de actualización obligatoria quedaba sin botón
        /// de descarga. La dirección pública del sitio es la misma que usa la
        /// recuperación de contraseña (PasswordRecovery:PublicBaseUrl).
        /// </summary>
        internal string ResolveDownloadUrl(string? urlDescarga)
        {
            if (!string.IsNullOrWhiteSpace(urlDescarga)) return urlDescarga;

            var baseUrl = _configuration["PasswordRecovery:PublicBaseUrl"]?.TrimEnd('/');
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var site) &&
                string.Equals(site.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return baseUrl + "/MobileApp/Download";
            }

            return string.Empty;
        }
    }
}
