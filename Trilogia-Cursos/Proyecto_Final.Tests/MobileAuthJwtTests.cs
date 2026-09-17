using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Options;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Tests;

/// <summary>
/// Fase 1 de la aplicación móvil: emisión y validación de JWT.
/// El eje de estas pruebas es que la autorización nueva sea estrictamente
/// aditiva y que ninguna decisión de acceso quede del lado del cliente.
/// </summary>
public sealed class MobileAuthJwtTests
{
    private const string Issuer = "https://pruebas.example.test/";
    private const string Audience = "distribuidorajj.movil";
    private static readonly string SigningKey = new('k', 48);

    // El valor concreto es irrelevante: ValidateUserAsync está simulado y nunca
    // compara nada. Se construye en vez de escribirse como literal para que el
    // escáner de secretos del repositorio no tenga que distinguir entre una
    // credencial de prueba y una real — esa distinción es justo la que no
    // conviene que un escáner de seguridad tenga que hacer.
    private static readonly string AnyCredential = new('x', 16);

    // Igual que AnyCredential: valores opacos construidos, no literales. Los
    // servicios están simulados y ninguno inspecciona el contenido.
    private static readonly string AnyRefreshToken = new('r', 24);
    private static readonly string StolenRefreshToken = new('s', 24);

    // ── Contrato de login: lo que ya existía no se mueve ────────────────────

    [Fact]
    public void AuthResult_ConservaLosSeisCamposDelContratoOriginal()
    {
        var properties = typeof(AuthResult).GetProperties().Select(property => property.Name).ToArray();

        Assert.Contains(nameof(AuthResult.Success), properties);
        Assert.Contains(nameof(AuthResult.Message), properties);
        Assert.Contains(nameof(AuthResult.UserId), properties);
        Assert.Contains(nameof(AuthResult.FullName), properties);
        Assert.Contains(nameof(AuthResult.Email), properties);
        Assert.Contains(nameof(AuthResult.Role), properties);
    }

    [Fact]
    public async Task Login_SinDeviceId_NoEmiteTokens_YDejaIntactoElFlujoDelMvc()
    {
        var refreshTokens = new Mock<IRefreshTokenService>(MockBehavior.Strict);
        var controller = CreateAuthController(refreshTokens: refreshTokens);

        var result = await controller.Login(new LoginApiRequest
        {
            Email = "chofer@example.test",
            Password = AnyCredential
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AuthResult>(ok.Value);

        Assert.True(payload.Success);
        Assert.Equal(7, payload.UserId);
        Assert.Equal("Chofer", payload.Role);

        // Lo que consume el MVC sigue completo y los campos móviles vienen nulos.
        Assert.Null(payload.AccessToken);
        Assert.Null(payload.RefreshToken);
        Assert.Null(payload.Permissions);

        // MockBehavior.Strict: cualquier llamada al servicio de refresco habría
        // fallado la prueba. El login web no escribe filas de token.
        refreshTokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Login_ConDeviceId_DeUnPerfilSinAccesoMovil_NoEmiteTokens()
    {
        // Un cliente tiene credenciales válidas pero no usa la aplicación. Antes
        // recibía tokens y quedaba en una pantalla que fallaba en cada consulta.
        var refreshTokens = new Mock<IRefreshTokenService>(MockBehavior.Strict);
        var controller = CreateAuthController(refreshTokens, role: "Cliente", permissions: Array.Empty<string>());

        var result = await controller.Login(new LoginApiRequest
        {
            Email = "cliente@example.test",
            Password = AnyCredential,
            DeviceId = "dispositivo-de-prueba"
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        var payload = Assert.IsType<AuthResult>(forbidden.Value);
        Assert.False(payload.Success);
        Assert.Null(payload.AccessToken);
        Assert.Null(payload.RefreshToken);
        refreshTokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Login_SinDeviceId_DeUnCliente_SigueFuncionandoParaElSitioWeb()
    {
        var controller = CreateAuthController(role: "Cliente", permissions: Array.Empty<string>());

        var result = await controller.Login(new LoginApiRequest
        {
            Email = "cliente@example.test",
            Password = AnyCredential
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_ConDeviceId_EmiteAccessRefreshYPermisos()
    {
        var controller = CreateAuthController();

        var result = await controller.Login(new LoginApiRequest
        {
            Email = "chofer@example.test",
            Password = AnyCredential,
            DeviceId = "dispositivo-de-prueba",
            DeviceName = "Moto G"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AuthResult>(ok.Value);

        Assert.True(payload.Success);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.NotNull(payload.ExpiresAt);
        Assert.Contains("ENTREGAS_ACTUALIZAR_PROPIA", payload.Permissions!);

        // Los campos originales siguen presentes junto a los nuevos.
        Assert.Equal(7, payload.UserId);
        Assert.Equal("Chofer", payload.Role);
    }

    [Fact]
    public async Task Refresh_ConTokenInvalido_Devuelve401()
    {
        var refreshTokens = new Mock<IRefreshTokenService>();
        refreshTokens
            .Setup(service => service.ExchangeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshExchangeResult(RefreshOutcome.Invalid, null, null));

        var controller = CreateAuthController(refreshTokens: refreshTokens);

        var result = await controller.Refresh(new RefreshTokenApiRequest { RefreshToken = AnyRefreshToken });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_ConTokenReutilizado_RespondeIgualQueUnoInvalido()
    {
        var reused = new Mock<IRefreshTokenService>();
        reused
            .Setup(service => service.ExchangeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshExchangeResult(RefreshOutcome.Reused, null, null));

        var invalid = new Mock<IRefreshTokenService>();
        invalid
            .Setup(service => service.ExchangeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshExchangeResult(RefreshOutcome.Invalid, null, null));

        var reusedResult = await CreateAuthController(refreshTokens: reused)
            .Refresh(new RefreshTokenApiRequest { RefreshToken = AnyRefreshToken });
        var invalidResult = await CreateAuthController(refreshTokens: invalid)
            .Refresh(new RefreshTokenApiRequest { RefreshToken = AnyRefreshToken });

        // Distinguirlos le confirmaría a un atacante que el token robado era real.
        var reusedBody = Assert.IsType<UnauthorizedObjectResult>(reusedResult);
        var invalidBody = Assert.IsType<UnauthorizedObjectResult>(invalidResult);
        Assert.Equal(
            Assert.IsType<AuthResult>(invalidBody.Value).Message,
            Assert.IsType<AuthResult>(reusedBody.Value).Message);
    }

    [Fact]
    public async Task Logout_SiempreRespondeIgual_ExistaONoElToken()
    {
        var refreshTokens = new Mock<IRefreshTokenService>();
        var controller = CreateAuthController(refreshTokens: refreshTokens);

        var conToken = await controller.Logout(new RefreshTokenApiRequest { RefreshToken = AnyRefreshToken });
        var sinToken = await controller.Logout(new RefreshTokenApiRequest { RefreshToken = string.Empty });

        Assert.IsType<OkObjectResult>(conToken);
        Assert.IsType<OkObjectResult>(sinToken);
        refreshTokens.Verify(
            service => service.RevokeAsync(AnyRefreshToken, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Emisión del access token ────────────────────────────────────────────

    [Fact]
    public void AccessToken_LlevaSubRolYPermisos_YCaducaSegunConfiguracion()
    {
        var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var service = new JwtTokenService(
            BuildOptions(),
            new FixedSigningKeyProvider(SigningKey),
            new StubTimeProvider(now));

        var issued = service.CreateAccessToken(
            42,
            "chofer@example.test",
            "Chofer de Prueba",
            "Chofer",
            new[] { "ENTREGAS_ACTUALIZAR_PROPIA", "FLOTA_KILOMETRAJE_PROPIO" });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.Value);

        Assert.Equal("42", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Chofer", token.Claims.Single(claim => claim.Type == ClaimTypes.Role).Value);
        Assert.Equal(Issuer, token.Issuer);
        Assert.Contains(Audience, token.Audiences);

        var permissions = token.Claims
            .Where(claim => claim.Type == JwtTokenService.PermissionClaimType)
            .Select(claim => claim.Value)
            .ToArray();
        Assert.Equal(2, permissions.Length);
        Assert.Contains("FLOTA_KILOMETRAJE_PROPIO", permissions);

        Assert.Equal(now.AddMinutes(30), issued.ExpiresAt);
    }

    [Fact]
    public void AccessToken_FirmadoConOtraClave_NoValida()
    {
        var service = new JwtTokenService(
            BuildOptions(),
            new FixedSigningKeyProvider(SigningKey),
            new StubTimeProvider(DateTimeOffset.UtcNow));

        var issued = service.CreateAccessToken(1, "a@b.test", "A", "Chofer", Array.Empty<string>());

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('z', 48))),
            ValidateLifetime = false
        };

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(issued.Value, parameters, out _));
    }

    // ── Clave de firma ──────────────────────────────────────────────────────

    [Fact]
    public void ClaveDeFirma_FaltanteFueraDeDevelopment_ImpideArrancar()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new JwtSigningKeyProvider(
                BuildOptions(signingKey: string.Empty),
                new StubEnvironment(Environments.Production),
                NullLogger<JwtSigningKeyProvider>.Instance));

        Assert.Contains("Jwt:SigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClaveDeFirma_DemasiadoCorta_SeRechazaInclusoEnDevelopment()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new JwtSigningKeyProvider(
                BuildOptions(signingKey: "corta"),
                new StubEnvironment(Environments.Development),
                NullLogger<JwtSigningKeyProvider>.Instance));
    }

    [Fact]
    public void ClaveDeFirma_AusenteEnDevelopment_GeneraUnaEfimeraDistintaPorProceso()
    {
        var first = new JwtSigningKeyProvider(
            BuildOptions(signingKey: string.Empty),
            new StubEnvironment(Environments.Development),
            NullLogger<JwtSigningKeyProvider>.Instance);
        var second = new JwtSigningKeyProvider(
            BuildOptions(signingKey: string.Empty),
            new StubEnvironment(Environments.Development),
            NullLogger<JwtSigningKeyProvider>.Instance);

        // Nunca hay una clave por defecto compartida, que es el riesgo real.
        Assert.NotEqual(first.Key.Key, second.Key.Key);
        Assert.True(first.Key.KeySize >= JwtOptions.MinimumSigningKeyBytes * 8);
    }

    // ── Tokens de refresco ──────────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_SeGuardaComoSha256_NuncaEnClaro()
    {
        RefreshTokenIssueRequest? captured = null;
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(service => service.IssueRefreshTokenAsync(It.IsAny<RefreshTokenIssueRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshTokenIssueRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(Guid.NewGuid());

        var service = BuildRefreshTokenService(db);
        var issued = await service.IssueAsync(7, "dispositivo", "Moto G", "10.0.0.1");

        Assert.NotNull(captured);
        Assert.NotEqual(issued.Value, captured!.TokenHash);
        Assert.Equal(64, captured.TokenHash.Length);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Value))),
            captured.TokenHash);
    }

    [Fact]
    public async Task RefreshToken_CanjeCorrecto_DevuelveUsuarioYTokenNuevoDistinto()
    {
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(service => service.ExchangeRefreshTokenAsync(It.IsAny<RefreshTokenExchangeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshExchangeRow("Ok", 7, Guid.NewGuid()));
        db.Setup(service => service.GetActiveUserAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiUser { UsuarioId = 7, PerfilNombre = "Chofer", Activo = true });

        var result = await BuildRefreshTokenService(db).ExchangeAsync(AnyRefreshToken, null);

        Assert.Equal(RefreshOutcome.Ok, result.Outcome);
        Assert.Equal(7, result.User!.UsuarioId);
        Assert.NotEqual(AnyRefreshToken, result.RefreshToken!.Value);
    }

    [Fact]
    public async Task RefreshToken_Reutilizado_NoDevuelveUsuarioNiTokenNuevo()
    {
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(service => service.ExchangeRefreshTokenAsync(It.IsAny<RefreshTokenExchangeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshExchangeRow("Reutilizado", 7, Guid.NewGuid()));

        var result = await BuildRefreshTokenService(db).ExchangeAsync(StolenRefreshToken, null);

        Assert.Equal(RefreshOutcome.Reused, result.Outcome);
        Assert.Null(result.User);
        Assert.Null(result.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_DeUsuarioDesactivado_RevocaLaCadenaReciénEmitida()
    {
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(service => service.ExchangeRefreshTokenAsync(It.IsAny<RefreshTokenExchangeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshExchangeRow("Ok", 7, Guid.NewGuid()));
        db.Setup(service => service.GetActiveUserAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiUser?)null);

        var result = await BuildRefreshTokenService(db).ExchangeAsync(AnyRefreshToken, null);

        Assert.Equal(RefreshOutcome.Invalid, result.Outcome);
        db.Verify(
            service => service.RevokeChainAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Filtro de permisos ──────────────────────────────────────────────────

    [Fact]
    public async Task Permisos_SinAutenticar_Devuelve401()
    {
        var db = new Mock<IMobileAuthDbService>(MockBehavior.Strict);
        var context = BuildFilterContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await BuildPermissionFilter(db).OnAuthorizationAsync(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Permisos_Administrador_PasaSinConsultarLaBase()
    {
        var db = new Mock<IMobileAuthDbService>(MockBehavior.Strict);
        var context = BuildFilterContext(BuildPrincipal("Administrador"));

        await BuildPermissionFilter(db).OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Permisos_ClaimPresenteEnElTokenPeroRevocadoEnLaBase_Devuelve403()
    {
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(service => service.HasPermissionAsync("Chofer", "FLOTA_KILOMETRAJE_PROPIO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // El token todavía afirma tener el permiso: la base manda igual.
        var principal = BuildPrincipal("Chofer", "FLOTA_KILOMETRAJE_PROPIO");
        var context = BuildFilterContext(principal);

        await BuildPermissionFilter(db).OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Permisos_ConPermisoVigente_DejaPasar()
    {
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(service => service.HasPermissionAsync("Chofer", "FLOTA_KILOMETRAJE_PROPIO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = BuildFilterContext(BuildPrincipal("Chofer"));

        await BuildPermissionFilter(db).OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Permisos_SiLaBaseFalla_NiegaElAcceso()
    {
        var db = new Mock<IMobileAuthDbService>();
        db.Setup(service => service.HasPermissionAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("base dormida"));

        var context = BuildFilterContext(BuildPrincipal("Chofer"));

        await BuildPermissionFilter(db).OnAuthorizationAsync(context);

        // Fallar abierto en autorización nunca es una opción.
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    // ── Ayudantes ───────────────────────────────────────────────────────────

    private static IOptions<JwtOptions> BuildOptions(string? signingKey = null) =>
        Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = signingKey ?? SigningKey,
            AccessTokenMinutes = 30,
            RefreshTokenDays = 14
        });

    private static RefreshTokenService BuildRefreshTokenService(Mock<IMobileAuthDbService> db) =>
        new(db.Object,
            BuildOptions(),
            new StubTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<RefreshTokenService>.Instance);

    private static RequirePermissionFilter BuildPermissionFilter(Mock<IMobileAuthDbService> db) =>
        new("FLOTA_KILOMETRAJE_PROPIO", db.Object, NullLogger<RequirePermissionFilter>.Instance);

    private static ClaimsPrincipal BuildPrincipal(string role, params string[] permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        claims.AddRange(permissions.Select(permission => new Claim(JwtTokenService.PermissionClaimType, permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private static AuthorizationFilterContext BuildFilterContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static AuthController CreateAuthController(
        Mock<IRefreshTokenService>? refreshTokens = null,
        string role = "Chofer",
        string[]? permissions = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var accounts = new Mock<IAccountApiDbService>();
        accounts
            .Setup(service => service.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiUser
            {
                UsuarioId = 7,
                NombreCompleto = "Chofer de Prueba",
                Correo = "chofer@example.test",
                PerfilNombre = role,
                Activo = true
            });

        var mobileAuth = new Mock<IMobileAuthDbService>();
        mobileAuth
            .Setup(service => service.GetPermissionsForRoleAsync(role, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions ?? new[] { "MOVIL_ACCESO", "ENTREGAS_ACTUALIZAR_PROPIA" });

        if (refreshTokens is null)
        {
            refreshTokens = new Mock<IRefreshTokenService>();
            refreshTokens
                .Setup(service => service.IssueAsync(
                    It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IssuedRefreshToken(AnyRefreshToken, DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid()));
        }

        var jwt = new JwtTokenService(
            BuildOptions(),
            new FixedSigningKeyProvider(SigningKey),
            new StubTimeProvider(DateTimeOffset.UtcNow));

        return new AuthController(
            accounts.Object,
            new EmailService(configuration),
            new LoginAttemptLimiter(cache),
            new PasswordRecoveryAttemptLimiter(cache),
            configuration,
            NullLogger<AuthController>.Instance,
            jwt,
            refreshTokens.Object,
            mobileAuth.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private sealed class FixedSigningKeyProvider(string key) : IJwtSigningKeyProvider
    {
        public SymmetricSecurityKey Key { get; } = new(Encoding.UTF8.GetBytes(key));
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
