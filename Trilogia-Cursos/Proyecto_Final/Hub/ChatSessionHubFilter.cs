using Microsoft.AspNetCore.SignalR;
using Proyecto_Final.Middleware;

namespace Proyecto_Final.Hubs;

/// <summary>
/// Valida la sesión del usuario del chat al conectar y en cada invocación del hub. El hub lee la sesión del
/// HttpContext del primer request, así que sin esto una conexión abierta seguiría funcionando después de
/// inactivar al usuario o de cambiarle el perfil o la contraseña (sello de seguridad, migración 0311).
/// </summary>
public sealed class ChatSessionHubFilter(IChatConnectionRegistry registry, ILogger<ChatSessionHubFilter> logger) : IHubFilter
{
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var session = context.Context.GetHttpContext()?.Session;
        var userId = session?.GetInt32("UserId") ?? 0;
        var role = session?.GetString("UserRole") ?? string.Empty;
        var stamp = session?.GetString("SecurityStamp");

        if (userId <= 0 || !await IsCurrentAsync(context.ServiceProvider, userId, role, stamp, context.Context.ConnectionAborted))
        {
            logger.LogInformation("Se rechazó una conexión del chat sin sesión vigente (usuario {UserId}).", userId);
            context.Context.Abort();
            return;
        }

        registry.Register(context.Context, userId, role, stamp);
        await next(context);
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var connection = registry.Snapshot().FirstOrDefault(c => c.Context.ConnectionId == invocationContext.Context.ConnectionId);
        if (connection is null
            || !await IsCurrentAsync(invocationContext.ServiceProvider, connection.UserId, connection.Role, connection.SecurityStamp, invocationContext.Context.ConnectionAborted))
        {
            logger.LogInformation("Se cortó una conexión del chat: la sesión ya no está vigente.");
            invocationContext.Context.Abort();
            throw new HubException("La sesión ya no está vigente.");
        }

        return await next(invocationContext);
    }

    public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        registry.Unregister(context.Context.ConnectionId);
        return next(context, exception);
    }

    private async Task<bool> IsCurrentAsync(IServiceProvider services, int userId, string role, string? stamp, CancellationToken cancellationToken)
    {
        try
        {
            return await services.GetRequiredService<ISessionStatusService>().IsCurrentAsync(userId, role, stamp, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Fallar abierto en autorización nunca es una opción.
            logger.LogError(exception, "No se pudo verificar la sesión del usuario {UserId} del chat.", userId);
            return false;
        }
    }
}
