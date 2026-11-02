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
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
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
