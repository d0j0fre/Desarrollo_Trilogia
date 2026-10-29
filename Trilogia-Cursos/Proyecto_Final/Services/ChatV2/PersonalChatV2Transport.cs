using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Proyecto_Final.Services.ChatV2;

/// <summary>The HTTP session middleware and canonical SQL login remain the authentication authority.</summary>
internal sealed class PersonalChatV2Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, TimeProvider clock) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "ChatV2OriginalLogin";
    internal static bool OriginAllowed(HttpRequest request)
    {
        if (!request.IsHttps || request.Query.ContainsKey("access_token")) return false;
        if (!request.Headers.TryGetValue("Origin", out var origins)) return true; // Authenticated native clients have no browser Origin.
        return origins.Count == 1 && Uri.TryCreate(origins[0], UriKind.Absolute, out var origin)
            && origin.Scheme == Uri.UriSchemeHttps && origin.IsLoopback && origin.UserInfo.Length == 0
            && origin.AbsolutePath == "/" && origin.Query.Length == 0 && origin.Fragment.Length == 0
            && string.Equals(origin.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!OriginAllowed(Request) || Context.Items[OriginalLoginSession.ValidatedContextItem] is not OriginalLoginContext login
            || !login.HasCanonicalLifetime || login.ExpiresUtc <= clock.GetUtcNow().UtcDateTime)
            return Task.FromResult(AuthenticateResult.Fail("connection_denied"));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, login.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("original_login", login.LoginId.ToString("D"))], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Bounded, server-only original contexts; never emits or substitutes an authentication credential.</summary>
internal sealed class PersonalChatV2TransportSessions
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OriginalLoginContext> _connections = new(StringComparer.Ordinal);
    internal void Add(string connection, OriginalLoginContext login)
    {
        if (string.IsNullOrEmpty(connection) || !login.HasCanonicalLifetime) throw new ChatV2Exception(ChatV2Failure.Session);
        lock (_gate)
        {
            var same = _connections.Values.Where(x => x.LoginId == login.LoginId).ToArray();
            if (_connections.Count >= 1024 || same.Length >= 8 || _connections.ContainsKey(connection)) throw new ChatV2Exception(ChatV2Failure.Dependency);
            if (same.Any(x => x.UserId != login.UserId || x.ProfileId != login.ProfileId || x.SecurityStamp != login.SecurityStamp
                || x.IssuedUtc != login.IssuedUtc || x.ExpiresUtc != login.ExpiresUtc
                || !CryptographicOperations.FixedTimeEquals(x.BindingHash.Span, login.BindingHash.Span))) throw new ChatV2Exception(ChatV2Failure.Session);
            _connections.Add(connection, login with { BindingHash = login.BindingHash.ToArray() });
        }
    }
    internal OriginalLoginContext Capture(Guid session)
    {
        lock (_gate)
        {
            var login = _connections.Values.FirstOrDefault(x => x.LoginId == session) ?? throw new ChatV2Exception(ChatV2Failure.Session);
            return login with { BindingHash = login.BindingHash.ToArray() };
        }
    }
    internal void Remove(string connection)
    {
        lock (_gate) if (_connections.Remove(connection, out var login)) Clear(login);
    }
    internal static void Clear(OriginalLoginContext login)
    {
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(login.BindingHash, out ArraySegment<byte> bytes))
            CryptographicOperations.ZeroMemory(bytes.AsSpan());
    }
}

internal sealed class PersonalChatV2ConnectionValidator(PersonalChatV2TransportSessions sessions,
    IServiceScopeFactory scopes, TimeProvider time) : IChatV2ConnectionValidator
{
    public async ValueTask<ChatV2ConnectionContext> ValidateAsync(Guid session, ChatV2Resource? resource, CancellationToken ct)
    {
        var login = sessions.Capture(session);
        try
        {
            if (login.ExpiresUtc <= time.GetUtcNow().UtcDateTime) throw new ChatV2Exception(ChatV2Failure.Session);
            using var scope = scopes.CreateScope();
            var persistence = scope.ServiceProvider.GetRequiredService<IChatV2IntentPersistence>();
            var actor = await persistence.ResolveAsync(login, ct);
            if (actor.OriginalLoginId != login.LoginId || actor.SessionId != login.LoginId || actor.Actor != login.UserId
                || actor.Profile != login.ProfileId || actor.SecurityStamp != login.SecurityStamp || actor.ActorEpoch <= 0
                || actor.ExpiresUtc <= time.GetUtcNow() || actor.ExpiresUtc.UtcDateTime > login.ExpiresUtc)
                throw new ChatV2Exception(ChatV2Failure.Session);
            if (actor.Kind != ChatV2Kind.Internal || !actor.Capabilities.HasFlag(ChatV2Capabilities.InternalUse))
                throw new ChatV2Exception(ChatV2Failure.Forbidden);
            long epoch = 0;
            if (resource is ChatV2Resource selected)
            {
                selected.Validate();
                if (selected.Domain != ChatV2Domain.Private) throw new ChatV2Exception(ChatV2Failure.Forbidden);
                var access = await persistence.MembershipAsync(login, selected, ct);
                if (!access.CanRead || access.AuthorizationVersion < 0) throw new ChatV2Exception(ChatV2Failure.Resource);
                epoch = access.AuthorizationVersion;
            }
            return new(session, actor.Actor, actor.ActorEpoch, resource, epoch, actor.ExpiresUtc);
        }
        finally { PersonalChatV2TransportSessions.Clear(login); }
    }
}

[Authorize(AuthenticationSchemes = PersonalChatV2Authentication.SchemeName)]
public sealed class PersonalChatV2Hub : Hub
{
    private readonly ChatV2ConnectionRegistry _registry;
    private readonly PersonalChatV2TransportSessions _sessions;
    // The internal session store cannot be supplied by a Hub request.
    internal PersonalChatV2Hub(ChatV2ConnectionRegistry registry, PersonalChatV2TransportSessions sessions)
    { _registry = registry; _sessions = sessions; }
    public override async Task OnConnectedAsync()
    {
        try
        {
            var login = OriginalLoginSession.ValidatedRequest(Context.GetHttpContext() ?? throw new UnauthorizedAccessException());
            if (Context.User?.FindFirst("original_login")?.Value != login.LoginId.ToString("D")) throw new UnauthorizedAccessException();
            _sessions.Add(Context.ConnectionId, login);
            await _registry.RegisterAsync(Context.ConnectionId, login.LoginId, Context.Abort, Context.ConnectionAborted);
            await base.OnConnectedAsync();
        }
        catch { _registry.Disconnect(Context.ConnectionId); _sessions.Remove(Context.ConnectionId); Context.Abort(); throw new HubException("connection_denied"); }
    }
    public override async Task OnDisconnectedAsync(Exception? error)
    { _registry.Disconnect(Context.ConnectionId); _sessions.Remove(Context.ConnectionId); await base.OnDisconnectedAsync(error); }
    public Task Subscribe(string domain, int resource) => Sensitive(async () =>
    {
        if (domain != "P") throw new ChatV2Exception(ChatV2Failure.Forbidden);
        await _registry.SubscribeAsync(Context.ConnectionId, new(ChatV2Domain.Private, resource), Context.ConnectionAborted);
    });
    public Task Unsubscribe() => Sensitive(async () => await _registry.UnsubscribeAsync(Context.ConnectionId, Context.ConnectionAborted));
    public Task Revalidate() => Sensitive(async () => await _registry.RevalidateConnectionAsync(Context.ConnectionId, Context.ConnectionAborted));
    private static async Task Sensitive(Func<Task> action)
    {
        try { await action(); }
        catch { throw new HubException("unavailable"); }
    }
}

internal sealed class PersonalChatV2GenericNotifications(IHubContext<PersonalChatV2Hub> hub) : IChatV2GenericNotifications
{
    public ValueTask InvalidateAsync(string serverConnectionId, CancellationToken ct)
        => new(hub.Clients.Client(serverConnectionId).SendCoreAsync("Invalidate", [], ct));
}

public interface IPersonalChatV2InvalidationPublisher { void Publish(ChatV2Resource resource); }

/// <summary>Hints only. Durable message/receipt truth and recovery remain SQL plus the normal authorized HTTP polling.</summary>
internal sealed class PersonalChatV2NotificationPump(ChatV2ConnectionRegistry registry,
    ILogger<PersonalChatV2NotificationPump> logger) : BackgroundService, IPersonalChatV2InvalidationPublisher
{
    private readonly Channel<ChatV2Resource> _updates = Channel.CreateBounded<ChatV2Resource>(new BoundedChannelOptions(256)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    public void Publish(ChatV2Resource resource) { if (resource.Domain == ChatV2Domain.Private && resource.Id > 0) _updates.Writer.TryWrite(resource); }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await Task.WhenAll(ReadUpdates(stoppingToken), Sweep(stoppingToken));
    private async Task ReadUpdates(CancellationToken ct)
    {
        try
        {
            await foreach (var resource in _updates.Reader.ReadAllAsync(ct))
                try { await registry.NotifyResourceAsync(resource, ct); }
                catch (Exception) when (!ct.IsCancellationRequested) { logger.LogWarning("Chat live notification unavailable; authorized polling remains active."); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
    private async Task Sweep(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await registry.SweepAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
    public override async Task StopAsync(CancellationToken ct) { _updates.Writer.TryComplete(); await base.StopAsync(ct); }
}

public static class PersonalChatV2TransportIntegration
{
    public const string Path = "/hubs/chat-v2-personal";
    public static IServiceCollection AddPersonalChatV2Transport(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, PersonalChatV2Authentication>(PersonalChatV2Authentication.SchemeName, _ => { });
        services.AddSignalR(options => { options.EnableDetailedErrors = false; options.MaximumReceiveMessageSize = 4096; options.MaximumParallelInvocationsPerClient = 1; });
        services.AddSingleton<PersonalChatV2TransportSessions>();
        services.AddSingleton<IChatV2ConnectionValidator, PersonalChatV2ConnectionValidator>();
        services.AddSingleton<IChatV2GenericNotifications, PersonalChatV2GenericNotifications>();
        services.AddSingleton<ChatV2ConnectionRegistry>();
        services.AddTransient<PersonalChatV2Hub>(sp => new(sp.GetRequiredService<ChatV2ConnectionRegistry>(), sp.GetRequiredService<PersonalChatV2TransportSessions>()));
        services.AddSingleton<PersonalChatV2NotificationPump>();
        services.AddSingleton<IPersonalChatV2InvalidationPublisher>(sp => sp.GetRequiredService<PersonalChatV2NotificationPump>());
        services.AddHostedService(sp => sp.GetRequiredService<PersonalChatV2NotificationPump>());
        return services;
    }
}
