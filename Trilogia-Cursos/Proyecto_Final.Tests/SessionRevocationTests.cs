using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Proyecto_Final.Middleware;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Options;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Tests;

// Seguridad / revocación de sesiones (migración 0311): inactivar, cambiar de perfil o de contraseña
// cierra las sesiones abiertas de la web y los JWT de la aplicación móvil.
public sealed class SessionRevocationTests
{
    private const string Stamp = "6f0c1e5e-2b0b-4c1e-9a53-0d6f2f1d7a11";
    private const string OtherStamp = "0b7f7c3a-8d7f-4a27-b3e1-6a1d7f6a2c99";

    // ── Web: SessionStatusService ───────────────────────────────────────────

    [Fact]
    public void WebSession_IsCurrentOnlyWhenActiveSameRoleAndSameStamp()
    {
        var current = new UserSessionState(true, "Chofer", Stamp);

        Assert.True(SessionStatusService.IsCurrent(current, "Chofer", Stamp));
        Assert.True(SessionStatusService.IsCurrent(current, "chofer", Stamp.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(false, "Chofer", Stamp)]        // usuario o perfil inactivo
    [InlineData(true, "Vendedor", Stamp)]        // cambió de perfil
    [InlineData(true, "Chofer", OtherStamp)]     // cambió la contraseña (o se inactivó y reactivó)
    public void WebSession_IsRevokedWhenAnyAccessFactorChanged(bool active, string role, string stamp)
    {
        Assert.False(SessionStatusService.IsCurrent(new UserSessionState(active, role, stamp), "Chofer", Stamp));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WebSession_WithoutStampIsNotCurrent(string? sessionStamp)
    {
        Assert.False(SessionStatusService.IsCurrent(new UserSessionState(true, "Chofer", Stamp), "Chofer", sessionStamp));
        Assert.False(SessionStatusService.IsCurrent(null, "Chofer", Stamp));
    }

    [Fact]
    public async Task WebMiddleware_PassesTheStampStoredInTheSessionToTheService()
    {
        var status = new Mock<ISessionStatusService>();
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", Stamp, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        status.Setup(s => s.IsCurrentAsync(7, "Chofer", OtherStamp, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        async Task<(bool Called, HttpContext Context)> Run(string stamp)
        {
            var context = new DefaultHttpContext { Session = new FakeSession() };
            context.Session.SetInt32("UserId", 7);
            context.Session.SetString("UserRole", "Chofer");
            context.Session.SetString("SecurityStamp", stamp);
            var called = false;
            var middleware = new SessionStatusMiddleware(_ => { called = true; return Task.CompletedTask; }, NullLogger<SessionStatusMiddleware>.Instance);
            await middleware.InvokeAsync(context, status.Object);
            return (called, context);
        }

        var valid = await Run(Stamp);
        Assert.True(valid.Called);
        Assert.Equal(7, valid.Context.Session.GetInt32("UserId"));

        var revoked = await Run(OtherStamp);
        Assert.False(revoked.Called);
        Assert.Null(revoked.Context.Session.GetInt32("UserId"));
        Assert.Equal("/Account/Login", revoked.Context.Response.Headers.Location.ToString());
    }

    [Fact]
    public void WebLogin_StoresTheSecurityStampInTheSession()
    {
        var source = File.ReadAllText(SourcePath("Proyecto_Final", "Controllers", "AccountController.cs"));

        Assert.Contains("Session.SetString(\"SecurityStamp\"", source, StringComparison.Ordinal);
        Assert.Contains("GetStateAsync", source, StringComparison.Ordinal);
    }

    // ── API: sello en el JWT y revalidación en cada request ─────────────────

    [Fact]
    public void AccessToken_CarriesTheSecurityStampClaimOnlyWhenProvided()
    {
        var service = BuildTokenService();

        var withStamp = new JwtSecurityTokenHandler().ReadJwtToken(
            service.CreateAccessToken(7, "a@b.test", "A", "Chofer", Array.Empty<string>(), Stamp).Value);
        var without = new JwtSecurityTokenHandler().ReadJwtToken(
            service.CreateAccessToken(7, "a@b.test", "A", "Chofer", Array.Empty<string>()).Value);

        Assert.Equal(Stamp, withStamp.Claims.Single(c => c.Type == JwtTokenService.SecurityStampClaimType).Value);
        Assert.DoesNotContain(without.Claims, c => c.Type == JwtTokenService.SecurityStampClaimType);
    }

    [Fact]
    public async Task ApiToken_IsAcceptedWhileUserIsActiveWithTheSameRoleAndStamp()
    {
        var db = DbReturning(new ApiSessionState(true, "Chofer", Stamp));

        var result = await SessionStateValidator.ValidateAsync(Principal("7", "Chofer", Stamp), db.Object, null, default);

        Assert.Equal(SessionValidationResult.Valid, result);
    }

    [Theory]
    [InlineData(false, "Chofer", Stamp)]       // inactivar al usuario
    [InlineData(true, "Vendedor", Stamp)]       // cambiarle el rol
    [InlineData(true, "Chofer", OtherStamp)]    // cambiarle la contraseña
    public async Task ApiToken_IsRevokedWhenTheUserWasInactivatedOrChanged(bool active, string role, string stamp)
    {
        var db = DbReturning(new ApiSessionState(active, role, stamp));

        var result = await SessionStateValidator.ValidateAsync(Principal("7", "Chofer", Stamp), db.Object, null, default);

        Assert.Equal(SessionValidationResult.Revoked, result);
    }

    [Fact]
    public async Task ApiToken_OfADeletedUserIsRevoked()
    {
        var db = DbReturning(null);

        var result = await SessionStateValidator.ValidateAsync(Principal("7", "Chofer", Stamp), db.Object, null, default);

        Assert.Equal(SessionValidationResult.Revoked, result);
    }

    [Theory]
    [InlineData("7", "Chofer", null)]            // emitido antes de 0311: sin sello
    [InlineData("7", null, Stamp)]
    [InlineData("no-numero", "Chofer", Stamp)]
    [InlineData("0", "Chofer", Stamp)]
    public async Task ApiToken_WithoutTheRequiredClaimsIsRevokedWithoutQueryingTheDatabase(string? subject, string? role, string? stamp)
    {
        var db = new Mock<IMobileAuthDbService>(MockBehavior.Strict);

        var result = await SessionStateValidator.ValidateAsync(Principal(subject, role, stamp), db.Object, null, default);

        Assert.Equal(SessionValidationResult.Revoked, result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ApiToken_FailsClosedWhenTheDatabaseIsUnavailable()
    {
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(s => s.GetSessionStateAsync(7, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("sin base"));

        var result = await SessionStateValidator.ValidateAsync(Principal("7", "Chofer", Stamp), db.Object, null, default);

        Assert.Equal(SessionValidationResult.Unavailable, result);
    }

    // El pipeline real: la validación está enganchada a OnTokenValidated y el rechazo es 401 (o 503 sin base).
    [Fact]
    public async Task JwtPipeline_RejectsARevokedSessionWith401AndAnUnavailableDatabaseWith503()
    {
        var options = ConfigureBearer(DbReturning(new ApiSessionState(false, "Chofer", Stamp)));

        var revoked = await RunTokenValidated(options.Options, options.Services, Principal("7", "Chofer", Stamp));
        Assert.False(revoked.Result!.Succeeded);
        var challenge = await RunChallenge(options.Options, revoked.Result!.Failure);
        Assert.Equal(StatusCodes.Status401Unauthorized, challenge.Response.StatusCode);
        Assert.Contains("no_autenticado", ReadBody(challenge), StringComparison.Ordinal);

        var unavailable = new Mock<IMobileAuthDbService>();
        unavailable.Setup(s => s.GetSessionStateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var broken = ConfigureBearer(unavailable);
        var failed = await RunTokenValidated(broken.Options, broken.Services, Principal("7", "Chofer", Stamp));
        Assert.False(failed.Result!.Succeeded);
        var unavailableChallenge = await RunChallenge(broken.Options, failed.Result!.Failure);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailableChallenge.Response.StatusCode);
        Assert.Contains("servicio_no_disponible", ReadBody(unavailableChallenge), StringComparison.Ordinal);
    }

    [Fact]
    public async Task JwtPipeline_AcceptsAValidSession()
    {
        var bearer = ConfigureBearer(DbReturning(new ApiSessionState(true, "Chofer", Stamp)));

        var accepted = await RunTokenValidated(bearer.Options, bearer.Services, Principal("7", "Chofer", Stamp));

        Assert.Null(accepted.Result);
    }

    [Fact]
    public void ApiLogin_EmbedsTheStampInEveryAccessToken()
    {
        var source = File.ReadAllText(SourcePath("Proyecto_FinalAPI", "Controllers", "AuthController.cs"));

        Assert.Equal(2, source.Split("user.SecurityStamp").Length - 1);
    }

    // ── Base de datos ───────────────────────────────────────────────────────

    [Fact]
    public void Migration0311_RenewsTheStampAndRevokesRefreshTokensOnAccessChanges()
    {
        var sql = File.ReadAllText(SourcePath("database", "migrations", "0311_revocacion_sesiones.sql"));

        Assert.Contains("SecurityStamp UNIQUEIDENTIFIER NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER TRIGGER dbo.tr_Usuarios_RevocarSesiones", sql, StringComparison.Ordinal);
        foreach (var column in new[] { "Activo", "PerfilId", "ContrasenaHash", "Contrasena" })
            Assert.Contains($"UPDATE({column})", sql, StringComparison.Ordinal);
        Assert.Contains("UPDATE token", sql, StringComparison.Ordinal);
        Assert.Contains("RevocadoUtc = SYSUTCDATETIME()", sql, StringComparison.Ordinal);
        Assert.Contains("sp_Auth_GetSessionState", sql, StringComparison.Ordinal);
        Assert.Contains("ISNULL(pf.Activo, 0) = 1", sql, StringComparison.Ordinal);
    }

    // ── Ayudantes ───────────────────────────────────────────────────────────

    private static Mock<IMobileAuthDbService> DbReturning(ApiSessionState? state)
    {
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(s => s.GetSessionStateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        return db;
    }

    private static ClaimsPrincipal Principal(string? subject, string? role, string? stamp)
    {
        var claims = new List<Claim>();
        if (subject is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (stamp is not null) claims.Add(new Claim(JwtTokenService.SecurityStampClaimType, stamp));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private static JwtTokenService BuildTokenService()
    {
        var key = new Mock<IJwtSigningKeyProvider>();
        key.SetupGet(k => k.Key).Returns(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 48))));
        return new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                Issuer = "https://pruebas.example.test/",
                Audience = "distribuidorajj.movil",
                SigningKey = new string('k', 48),
                AccessTokenMinutes = 30,
                RefreshTokenDays = 14
            }),
            key.Object,
            TimeProvider.System);
    }

    private static (JwtBearerOptions Options, IServiceProvider Services) ConfigureBearer(Mock<IMobileAuthDbService> db)
    {
        var key = new Mock<IJwtSigningKeyProvider>();
        key.SetupGet(k => k.Key).Returns(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 48))));
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        var configure = new ConfigureJwtBearerOptions(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                Issuer = "https://pruebas.example.test/",
                Audience = "distribuidorajj.movil",
                SigningKey = new string('k', 48),
                AccessTokenMinutes = 30,
                RefreshTokenDays = 14,
                ClockSkewSeconds = 30
            }),
            key.Object,
            environment.Object);
        var options = new JwtBearerOptions();
        configure.Configure(JwtBearerDefaults.AuthenticationScheme, options);

        var services = new ServiceCollection();
        services.AddSingleton(db.Object);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        return (options, services.BuildServiceProvider());
    }

    private static async Task<TokenValidatedContext> RunTokenValidated(JwtBearerOptions options, IServiceProvider services, ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var context = new TokenValidatedContext(httpContext, scheme, options) { Principal = principal };
        await options.Events.TokenValidated(context);
        return context;
    }

    private static async Task<HttpContext> RunChallenge(JwtBearerOptions options, Exception? failure)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var context = new JwtBearerChallengeContext(httpContext, scheme, options, new AuthenticationProperties()) { AuthenticateFailure = failure };
        await options.Events.Challenge(context);
        return httpContext;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEnd();
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
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
