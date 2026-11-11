using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Proyecto_FinalAPI.Services;

namespace Proyecto_FinalAPI.Authorization
{
    public enum SessionValidationResult
    {
        Valid,
        Revoked,
        Unavailable
    }

    /// <summary>
    /// Revalida contra la base, en cada request, que la sesión que representa un JWT siga vigente:
    /// el usuario y su perfil están activos, el perfil es el mismo con el que se emitió el token y el
    /// sello de seguridad no cambió (inactivación, cambio de perfil o de contraseña, migración 0311).
    /// La firma y el vencimiento ya los validó el handler; esto cierra la ventana de vida del access token.
    /// </summary>
    public static class SessionStateValidator
    {
        public const string RevokedFailure = "sesion_revocada";
        public const string UnavailableFailure = "estado_no_disponible";

        public static async Task<SessionValidationResult> ValidateAsync(
            ClaimsPrincipal principal,
            IMobileAuthDbService db,
            ILogger? logger,
            CancellationToken cancellationToken)
        {
            var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            var stamp = principal.FindFirstValue(JwtTokenService.SecurityStampClaimType);

            // Un token sin sello (emitido antes de 0311) no se acepta: la app lo renueva con su token de refresco.
            if (!int.TryParse(subject, out var userId) || userId <= 0
                || string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(stamp))
            {
                return SessionValidationResult.Revoked;
            }

            ApiSessionState? state;
            try
            {
                state = await db.GetSessionStateAsync(userId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Fallar abierto en autorización nunca es una opción: sin poder verificar, no se autoriza.
                logger?.LogError(exception, "No se pudo verificar el estado de la sesión del usuario {UserId}.", userId);
                return SessionValidationResult.Unavailable;
            }

            if (state is null || !state.Active
                || !string.Equals(state.Role, role, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(state.SecurityStamp, stamp, StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogInformation("Se rechazó un token de una sesión revocada o desactualizada del usuario {UserId}.", userId);
                return SessionValidationResult.Revoked;
            }

            return SessionValidationResult.Valid;
        }
    }
}
