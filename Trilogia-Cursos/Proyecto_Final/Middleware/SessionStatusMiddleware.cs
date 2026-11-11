using Microsoft.Data.SqlClient;
using System.Data;

namespace Proyecto_Final.Middleware;

public interface ISessionStatusService
{
    Task<bool> IsCurrentAsync(int userId, string role, CancellationToken cancellationToken);
}

public sealed class SessionStatusService(IConfiguration configuration) : ISessionStatusService
{
    public async Task<bool> IsCurrentAsync(int userId, string role, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand(@"SELECT COUNT(1) FROM dbo.Usuarios u
            INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
            WHERE u.UsuarioId = @UserId AND u.Activo = 1 AND p.Activo = 1 AND p.Nombre = @Role", connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@Role", SqlDbType.NVarChar, 100).Value = role;
        await connection.OpenAsync(cancellationToken);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
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
            try { valid = await status.IsCurrentAsync(id.Value, role, context.RequestAborted); }
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
