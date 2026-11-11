using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Hubs;
using Proyecto_Final.Middleware;

namespace Proyecto_Final.Tests;

// Seguridad / chat (SignalR) de un usuario con sesión revocada: cuando cambia el sello de seguridad
// (inactivar, cambiar de perfil o de contraseña) la conexión deja de enviar y de recibir.
public sealed class ChatSessionRevocationTests
{
    private const string Stamp = "6f0c1e5e-2b0b-4c1e-9a53-0d6f2f1d7a11";
    private const string OtherStamp = "0b7f7c3a-8d7f-4a27-b3e1-6a1d7f6a2c99";

    // ── Al conectar ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Connect_WithACurrentSession_RegistersTheConnectionAndContinues()
    {
        var (filter, registry, status) = Build();
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", Stamp, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var caller = new FakeCaller("c1", Session(7, "Chofer", Stamp));
        var continued = false;

        await filter.OnConnectedAsync(Lifetime(caller, status), _ => { continued = true; return Task.CompletedTask; });

        Assert.True(continued);
        Assert.Equal(0, caller.Aborts);
        Assert.Single(registry.Snapshot());
    }

    [Theory]
    [InlineData(0, "Chofer", Stamp)]          // sin sesión
    [InlineData(7, "Chofer", null)]           // sesión anterior a la migración 0311: sin sello
    public async Task Connect_WithoutAUsableSession_IsAbortedAndNeverRegistered(int userId, string role, string? stamp)
    {
        var (filter, registry, status) = Build();
        status.Setup(s => s.IsCurrentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string _, string? s, CancellationToken _) => !string.IsNullOrWhiteSpace(s));
        var caller = new FakeCaller("c1", Session(userId, role, stamp));
        var continued = false;

        await filter.OnConnectedAsync(Lifetime(caller, status), _ => { continued = true; return Task.CompletedTask; });

        Assert.False(continued);
        Assert.Equal(1, caller.Aborts);
        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public async Task Connect_WithARevokedSession_IsAborted()
    {
        var (filter, registry, status) = Build();
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", Stamp, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var caller = new FakeCaller("c1", Session(7, "Chofer", Stamp));
        var continued = false;

        await filter.OnConnectedAsync(Lifetime(caller, status), _ => { continued = true; return Task.CompletedTask; });

        Assert.False(continued);
        Assert.Equal(1, caller.Aborts);
        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public async Task Connect_FailsClosedWhenTheDatabaseIsUnavailable()
    {
        var (filter, registry, status) = Build();
        status.Setup(s => s.IsCurrentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sin base"));
        var caller = new FakeCaller("c1", Session(7, "Chofer", Stamp));

        await filter.OnConnectedAsync(Lifetime(caller, status), _ => Task.CompletedTask);

        Assert.Equal(1, caller.Aborts);
        Assert.Empty(registry.Snapshot());
    }

    // ── Al enviar (cada invocación del hub) ─────────────────────────────────

    [Fact]
    public async Task Invoke_WhileTheSessionIsCurrent_RunsTheMethod()
    {
        var (filter, registry, status) = Build();
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", Stamp, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var caller = new FakeCaller("c1", Session(7, "Chofer", Stamp));
        registry.Register(caller, 7, "Chofer", Stamp);

        var result = await filter.InvokeMethodAsync(Invocation(caller, status), _ => ValueTask.FromResult<object?>("ok"));

        Assert.Equal("ok", result);
        Assert.Equal(0, caller.Aborts);
    }

    [Fact]
    public async Task Invoke_AfterTheStampChanged_IsRefusedAndTheConnectionIsCut()
    {
        var (filter, registry, status) = Build();
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", Stamp, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var caller = new FakeCaller("c1", Session(7, "Chofer", Stamp));
        registry.Register(caller, 7, "Chofer", Stamp);
        var ran = false;

        await Assert.ThrowsAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(Invocation(caller, status), _ => { ran = true; return ValueTask.FromResult<object?>(null); }));

        Assert.False(ran);
        Assert.Equal(1, caller.Aborts);
    }

    [Fact]
    public async Task Invoke_FromAnUnregisteredConnection_IsRefused()
    {
        var (filter, _, status) = Build();
        var caller = new FakeCaller("desconocida", Session(7, "Chofer", Stamp));

        await Assert.ThrowsAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(Invocation(caller, status), _ => ValueTask.FromResult<object?>(null)));

        Assert.Equal(1, caller.Aborts);
        status.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Disconnect_RemovesTheConnectionFromTheRegistry()
    {
        var (filter, registry, status) = Build();
        var caller = new FakeCaller("c1", Session(7, "Chofer", Stamp));
        registry.Register(caller, 7, "Chofer", Stamp);

        await filter.OnDisconnectedAsync(Lifetime(caller, status), null, (_, _) => Task.CompletedTask);

        Assert.Empty(registry.Snapshot());
    }

    // ── Quien solo recibe: revalidación periódica ───────────────────────────

    [Fact]
    public async Task Worker_CutsOnlyTheConnectionsOfRevokedSessions()
    {
        var registry = new ChatConnectionRegistry();
        var revoked = new FakeCaller("a", Session(7, "Chofer", Stamp));
        var revokedSecondTab = new FakeCaller("b", Session(7, "Chofer", Stamp));
        var healthy = new FakeCaller("c", Session(8, "Vendedor", OtherStamp));
        registry.Register(revoked, 7, "Chofer", Stamp);
        registry.Register(revokedSecondTab, 7, "Chofer", Stamp);
        registry.Register(healthy, 8, "Vendedor", OtherStamp);
        var status = new Mock<ISessionStatusService>();
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", Stamp, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        status.Setup(s => s.IsCurrentAsync(8, "Vendedor", OtherStamp, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Worker(registry).CheckOnceAsync(status.Object, default);

        Assert.Equal([7], result);
        Assert.Equal(1, revoked.Aborts);
        Assert.Equal(1, revokedSecondTab.Aborts);
        Assert.Equal(0, healthy.Aborts);
        Assert.Equal(["c"], registry.Snapshot().Select(c => c.Context.ConnectionId));
    }

    [Fact]
    public async Task Worker_ChecksEachSessionSeparately_AnOldStampDoesNotSurviveBecauseTheNewOneIsValid()
    {
        var registry = new ChatConnectionRegistry();
        var oldTab = new FakeCaller("old", Session(7, "Chofer", OtherStamp));
        var newTab = new FakeCaller("new", Session(7, "Chofer", Stamp));
        registry.Register(oldTab, 7, "Chofer", OtherStamp);
        registry.Register(newTab, 7, "Chofer", Stamp);
        var status = new Mock<ISessionStatusService>();
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", Stamp, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", OtherStamp, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Worker(registry).CheckOnceAsync(status.Object, default);

        Assert.Equal(1, oldTab.Aborts);
        Assert.Equal(0, newTab.Aborts);
    }

    [Fact]
    public async Task Worker_DoesNotDisconnectAnyoneOnATransientDatabaseFailure()
    {
        var registry = new ChatConnectionRegistry();
        var caller = new FakeCaller("a", Session(7, "Chofer", Stamp));
        registry.Register(caller, 7, "Chofer", Stamp);
        var status = new Mock<ISessionStatusService>();
        status.Setup(s => s.IsCurrentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var result = await Worker(registry).CheckOnceAsync(status.Object, default);

        Assert.Empty(result);
        Assert.Equal(0, caller.Aborts);
        Assert.Single(registry.Snapshot());
    }

    // ── Cableado ────────────────────────────────────────────────────────────

    [Fact]
    public void Program_RegistersTheHubFilterTheRegistryAndTheWorker()
    {
        var program = File.ReadAllText(SourcePath("Proyecto_Final", "Program.cs"));

        Assert.Contains("AddSignalR().AddHubOptions<ChatHub>(options => options.AddFilter<ChatSessionHubFilter>())", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IChatConnectionRegistry, ChatConnectionRegistry>()", program, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<ChatSessionRevocationWorker>()", program, StringComparison.Ordinal);
    }

    // ── Ayudantes ───────────────────────────────────────────────────────────

    private static (ChatSessionHubFilter Filter, ChatConnectionRegistry Registry, Mock<ISessionStatusService> Status) Build()
    {
        var registry = new ChatConnectionRegistry();
        return (new ChatSessionHubFilter(registry, NullLogger<ChatSessionHubFilter>.Instance), registry, new Mock<ISessionStatusService>());
    }

    private static ChatSessionRevocationWorker Worker(ChatConnectionRegistry registry) =>
        new(registry, new Mock<IServiceScopeFactory>().Object, new ConfigurationBuilder().Build(), NullLogger<ChatSessionRevocationWorker>.Instance);

    private static ISession Session(int userId, string role, string? stamp)
    {
        var session = new FakeSession();
        if (userId > 0) session.SetInt32("UserId", userId);
        session.SetString("UserRole", role);
        if (stamp is not null) session.SetString("SecurityStamp", stamp);
        return session;
    }

    private static HubLifetimeContext Lifetime(FakeCaller caller, Mock<ISessionStatusService> status) =>
        new(caller, Services(status), new TestHub());

    private static HubInvocationContext Invocation(FakeCaller caller, Mock<ISessionStatusService> status) =>
        new(caller, Services(status), new TestHub(), typeof(TestHub).GetMethod(nameof(TestHub.Send))!, []);

    private static IServiceProvider Services(Mock<ISessionStatusService> status) =>
        new ServiceCollection().AddSingleton(status.Object).BuildServiceProvider();

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
    }

    private sealed class TestHub : Hub
    {
        public Task Send() => Task.CompletedTask;
    }

    private sealed class HttpContextFeature(HttpContext context) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private sealed class FakeCaller : HubCallerContext
    {
        private readonly string _connectionId;
        private readonly FeatureCollection _features = new();
        private readonly Dictionary<object, object?> _items = new();

        public FakeCaller(string connectionId, ISession session)
        {
            _connectionId = connectionId;
            _features.Set<IHttpContextFeature>(new HttpContextFeature(new DefaultHttpContext { Session = session }));
        }

        public int Aborts { get; private set; }
        public override string ConnectionId => _connectionId;
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items => _items;
        public override IFeatureCollection Features => _features;
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() => Aborts++;
    }

    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();
        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
