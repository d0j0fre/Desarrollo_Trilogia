using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Proyecto_Final.Middleware;

namespace Proyecto_Final.Hubs;

/// <summary>
/// Conexiones del chat abiertas, con el usuario, el perfil y el sello de seguridad con que iniciaron sesión.
/// Sirve para cortar las conexiones de un usuario cuya sesión se revocó (inactivado, cambio de perfil o de
/// contraseña): una conexión WebSocket ya establecida no vuelve a pasar por SessionStatusMiddleware.
/// </summary>
public interface IChatConnectionRegistry
{
    void Register(HubCallerContext context, int userId, string role, string? securityStamp);
    void Unregister(string connectionId);
    IReadOnlyList<ChatConnection> Snapshot();
    void Abort(ChatConnection connection);
}

public sealed record ChatConnection(HubCallerContext Context, int UserId, string Role, string? SecurityStamp);

public sealed class ChatConnectionRegistry : IChatConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ChatConnection> _connections = new();

    public void Register(HubCallerContext context, int userId, string role, string? securityStamp) =>
        _connections[context.ConnectionId] = new ChatConnection(context, userId, role, securityStamp);

    public void Unregister(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlyList<ChatConnection> Snapshot() => _connections.Values.ToList();

    /// <summary>Corta la conexión y la saca del registro.</summary>
    public void Abort(ChatConnection connection)
    {
        connection.Context.Abort();
        _connections.TryRemove(connection.Context.ConnectionId, out _);
    }
}

/// <summary>
/// Revalida cada cierto tiempo el estado de los usuarios con conexión de chat abierta y corta las de los que ya no
/// tienen una sesión vigente. Complementa al filtro del hub, que valida al conectar y en cada invocación, pero no
/// alcanza a quien solo recibe mensajes.
/// </summary>
public sealed class ChatSessionRevocationWorker : BackgroundService
{
    private readonly IChatConnectionRegistry _registry;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ChatSessionRevocationWorker> _logger;
    private readonly TimeSpan _interval;

    public ChatSessionRevocationWorker(
        IChatConnectionRegistry registry,
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        ILogger<ChatSessionRevocationWorker> logger)
    {
        _registry = registry;
        _scopes = scopes;
        _logger = logger;
        var seconds = configuration.GetValue<int?>("Chat:RevocationCheckSeconds") ?? 15;
        _interval = TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 300));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await CheckOnceAsync(scope.ServiceProvider.GetRequiredService<ISessionStatusService>(), stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "No se pudo revalidar las conexiones del chat.");
            }
        }
    }

    /// <summary>Una pasada: devuelve los usuarios cuyas conexiones se cortaron.</summary>
    public async Task<IReadOnlyList<int>> CheckOnceAsync(ISessionStatusService status, CancellationToken cancellationToken)
    {
        var revoked = new List<int>();
        // Se valida cada combinación usuario + perfil + sello: una conexión vieja de un usuario que volvió a iniciar
        // sesión no debe sobrevivir porque la nueva sí es válida.
        foreach (var group in _registry.Snapshot().GroupBy(c => (c.UserId, c.Role, c.SecurityStamp)))
        {
            bool current;
            try
            {
                current = await status.IsCurrentAsync(group.Key.UserId, group.Key.Role, group.Key.SecurityStamp, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Una falla transitoria de la base no desconecta a nadie; el filtro del hub sí falla cerrado al invocar.
                _logger.LogWarning(exception, "No se pudo verificar el estado del usuario {UserId} del chat.", group.Key.UserId);
                continue;
            }

            if (!current)
            {
                foreach (var connection in group) _registry.Abort(connection);
                revoked.Add(group.Key.UserId);
                _logger.LogInformation("Se cortó la conexión del chat del usuario {UserId}: su sesión fue revocada.", group.Key.UserId);
            }
        }

        return revoked;
    }
}
