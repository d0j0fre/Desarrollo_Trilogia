using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Base de los controladores de <c>/api/mobile/v1</c>.
    ///
    /// Concentra dos cosas que no deben quedar al criterio de cada endpoint:
    /// leer la identidad <b>del token</b> y nunca de la petición, y devolver
    /// errores con una forma estable y sin detalle interno.
    /// </summary>
    [ApiController]
    public abstract class MobileControllerBase : ControllerBase
    {
        /// <summary>
        /// Identificador del usuario tomado del claim <c>sub</c>. Ningún endpoint
        /// móvil acepta un identificador de usuario por parámetro: ese fue
        /// exactamente el riesgo que documentó <c>docs/api-auth-futura.md</c>.
        /// </summary>
        protected int CurrentUserId
        {
            get
            {
                var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(raw, out var userId) ? userId : 0;
            }
        }

        protected string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? "Usuario";

        protected string CurrentUserEmail => User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty;

        protected string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

        protected string ClientUserAgent => Request.Headers.UserAgent.ToString();

        /// <summary>
        /// «No existe» y «no es suyo» responden igual a propósito: distinguirlos
        /// le permitiría a alguien mapear rutas ajenas probando identificadores.
        /// </summary>
        protected IActionResult NotFoundOrForbidden() =>
            NotFound(new MobileError("no_encontrado", "El recurso no existe o no está disponible para usted."));

        protected IActionResult BusinessRule(string message) =>
            UnprocessableEntity(new MobileError("regla_de_negocio", message));

        protected IActionResult Unavailable() =>
            StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MobileError("servicio_no_disponible", "El servicio no está disponible en este momento. Intentá de nuevo."));

        protected IActionResult InvalidRequest(string message) =>
            BadRequest(new MobileError("solicitud_invalida", message));

        /// <summary>
        /// Ejecuta una operación contra la base traduciendo sus fallas.
        ///
        /// Un error ≥ 50000 es una regla de negocio escrita para el usuario en
        /// el procedimiento ("No hay suficiente stock para esa salida."): se
        /// devuelve como 422 con ese mensaje, para que el teléfono lo muestre
        /// tal cual. Cualquier otro error de SQL es infraestructura y no se
        /// detalla.
        ///
        /// Los endpoints del chofer no usan esto a propósito: ahí un rechazo se
        /// responde como 404 para no revelar rutas ajenas.
        /// </summary>
        protected async Task<IActionResult> GuardAsync(
            ILogger logger,
            string operation,
            Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                logger.LogInformation(
                    "Regla de negocio en {Operacion} para el usuario {UserId}: {Numero}.",
                    operation,
                    CurrentUserId,
                    exception.Number);
                return BusinessRule(exception.Message);
            }
            catch (SqlException exception)
            {
                logger.LogError(exception, "Falló {Operacion} para el usuario {UserId}.", operation, CurrentUserId);
                return Unavailable();
            }
        }

        /// <summary>Las escrituras que la cola puede reintentar exigen su identificador.</summary>
        protected static bool TryReadSyncGuid(string? raw, out Guid syncGuid) =>
            Guid.TryParse(raw, out syncGuid) && syncGuid != Guid.Empty;
    }
}
