using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Middleware;
using Proyecto_FinalAPI.Options;
using Proyecto_FinalAPI.Services;
using Proyecto_FinalAPI.Services.Mobile;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAccountApiDbService, AccountApiDbService>();
builder.Services.AddSingleton<IPasswordHashService, PasswordHashService>();
builder.Services.AddScoped<ProductsApiDbService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<LoginAttemptLimiter>();
builder.Services.AddSingleton<PasswordRecoveryAttemptLimiter>();
builder.Services.AddSingleton(TimeProvider.System);

// ── Autorización de la aplicación móvil (Fase 1) ────────────────────────────
// Emisión y validación de JWT. Aditivo: los endpoints públicos de productos y
// el contrato de login que consume el MVC siguen exactamente igual.
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IJwtSigningKeyProvider, JwtSigningKeyProvider>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IMobileAuthDbService, MobileAuthDbService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

// Superficie móvil (Fase 2).
builder.Services.AddScoped<IDriverMobileDbService, DriverMobileDbService>();
builder.Services.AddScoped<IMobileAuditService, MobileAuditService>();
builder.Services.AddScoped<IAppReleaseDbService, AppReleaseDbService>();
builder.Services.AddScoped<IInventoryMobileDbService, InventoryMobileDbService>();
builder.Services.AddScoped<IOrdersMobileDbService, OrdersMobileDbService>();
builder.Services.AddScoped<IManagementMobileDbService, ManagementMobileDbService>();
builder.Services.AddScoped<IOfficeMobileDbService, OfficeMobileDbService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("password-recovery", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Las políticas móviles se particionan por usuario del token, no por IP.
    // Una flota entera detrás del NAT de un mismo operador celular comparte IP:
    // si se limitara por dirección, los choferes se bloquearían entre sí.
    options.AddPolicy("mobile-read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetMobilePartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("mobile-write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetMobilePartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                // Holgado a propósito: al recuperar señal, la cola offline puede
                // descargar de golpe una jornada entera de entregas.
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Proyecto_FinalAPI - Licorera La Bodega";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();

// La resolución de la clave de firma ocurre al arrancar, no en la primera
// petición: si falta configuración en producción, la API no levanta.
_ = app.Services.GetRequiredService<IJwtSigningKeyProvider>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Después de autenticar, a propósito: las políticas móviles se particionan por
// el usuario del token y antes de este punto HttpContext.User está vacío, con
// lo que toda la flota detrás de una misma IP caería en la misma partición.
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
{
    service = "Proyecto_FinalAPI",
    project = "DistribuidoraJJ - Licorera La Bodega",
    status = "OK",
    health = "/health"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "OK",
    service = "Proyecto_FinalAPI",
    build = BuildIdentity.CommitSha
}));

app.MapControllers();
app.Run();

static string GetMobilePartition(HttpContext httpContext)
{
    var userId = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                 ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    return !string.IsNullOrWhiteSpace(userId)
        ? $"user:{userId}"
        : $"ip:{httpContext.Connection.RemoteIpAddress}";
}
