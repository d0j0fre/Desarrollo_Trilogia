using Microsoft.Data.SqlClient;
using System.Data;

namespace Proyecto_Final.Middleware;

public sealed record UserSessionState(bool Active, string Role, string SecurityStamp);

public interface ISessionStatusService
{
    /// <summary>
    /// Verdadero si la sesión sigue vigente: el usuario y su perfil están activos, el perfil es el mismo con el
    /// que inició sesión y el sello de seguridad no cambió (inactivación, cambio de perfil o de contraseña).
    /// Una sesión sin sello (abierta antes de la migración 0311) no es vigente.
    /// </summary>
    Task<bool> IsCurrentAsync(int userId, string role, string? securityStamp, CancellationToken cancellationToken);

    /// <summary>Estado actual del usuario; el inicio de sesión guarda su sello en la sesión.</summary>
    Task<UserSessionState?> GetStateAsync(int userId, CancellationToken cancellationToken);
}

public sealed class SessionStatusService(IConfiguration configuration) : ISessionStatusService
{
    public async Task<bool> IsCurrentAsync(int userId, string role, string? securityStamp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(securityStamp)) return false;
        var state = await GetStateAsync(userId, cancellationToken);
        return IsCurrent(state, role, securityStamp);
    }

    public static bool IsCurrent(UserSessionState? state, string role, string? securityStamp) =>
        state is { Active: true }
        && !string.IsNullOrWhiteSpace(securityStamp)
        && string.Equals(state.Role, role, StringComparison.OrdinalIgnoreCase)
        && string.Equals(state.SecurityStamp, securityStamp, StringComparison.OrdinalIgnoreCase);

    public async Task<UserSessionState?> GetStateAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand("dbo.sp_Auth_GetSessionState", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = userId;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new UserSessionState(
            reader.GetBoolean(reader.GetOrdinal("Activo")),
            reader.GetString(reader.GetOrdinal("PerfilNombre")),
            reader.GetGuid(reader.GetOrdinal("SecurityStamp")).ToString("D"));
    }
}

public sealed class SessionStatusMiddleware(RequestDelegate next, ILogger<SessionStatusMiddleware> logger)
{
    internal const string UnavailablePage = """
        <!DOCTYPE html>
        <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Servicio temporalmente no disponible</title></head>
        <body style="font-family:system-ui,sans-serif;max-width:34rem;margin:4rem auto;padding:0 1rem;text-align:center">
        <h1 style="font-size:1.4rem">No podemos completar esta solicitud en este momento.</h1>
        <p>No fue posible conectar con el servicio. Sus datos no se modificaron. Intente nuevamente en unos minutos.</p>
        <p><a href="">Reintentar</a> · <a href="/">Ir al inicio</a></p>
        </body></html>
        """;

    public async Task InvokeAsync(HttpContext context, ISessionStatusService status)
    {
        var id = context.Session.GetInt32("UserId");
        if (id is > 0)
        {
            var role = context.Session.GetString("UserRole") ?? string.Empty;
            bool valid;
            try { valid = await status.IsCurrentAsync(id.Value, role, context.Session.GetString("SecurityStamp"), context.RequestAborted); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "No se pudo verificar el estado de la sesión.");
                // BUG-02-01: sin base de datos no hay layout ni menú que mostrar; una página sencilla en vez de una respuesta en blanco.
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(UnavailablePage, context.RequestAborted);
                return;
            }
            if (!valid)
            {
                context.Session.Clear();
                context.Response.Redirect("/Account/Login");
                return;
            }
        }
        await next(context);
    }
}
