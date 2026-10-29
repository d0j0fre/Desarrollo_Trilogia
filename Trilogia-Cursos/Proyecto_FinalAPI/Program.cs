using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Middleware;
using Proyecto_FinalAPI.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
StartupConfigurationValidator.Validate(builder.Configuration, builder.Environment);
OriginalLoginAuthorityConfiguration.Validate(builder.Configuration);
CommercialAuthorityBoundary.ValidateConfiguration(builder.Configuration);
ContactNotificationConfiguration.Validate(builder.Configuration);
InvoiceNotificationConfiguration.Validate(builder.Configuration);
DeliveryNotificationConfiguration.Validate(builder.Configuration);
RouteDispatchNotificationConfiguration.Validate(builder.Configuration);
if (builder.Configuration.GetValue<bool>("RouteDispatchNotifications:Enabled"))
    await RouteDispatchNotificationManifest.VerifyAsync(builder.Configuration);
if (builder.Configuration.GetValue<bool>("DeliveryNotifications:Enabled"))
    await DeliveryNotificationManifest.VerifyAsync(builder.Configuration);
if (builder.Configuration.GetValue<bool>("InvoiceNotifications:Enabled"))
    await InvoiceNotificationManifest.VerifyAsync(builder.Configuration);
if (builder.Configuration.GetValue<bool>("Commerce:Enabled"))
{
    builder.WebHost.ConfigureKestrel(options => options.ConfigureHttpsDefaults(https =>
        https.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.AllowCertificate));
    await DistribuidoraJJ.Security.RestrictedSqlGate.VerifyAsync(builder.Configuration,
        ("CommercialPricingAuthority", "Pricing"));
    await CommercialQuoteManifest.VerifyAsync(builder.Configuration.GetConnectionString("CommercialPricingAuthority")!);
}
if (builder.Configuration.GetValue<bool>("OriginalLogin:AuthorityEnabled"))
    await DistribuidoraJJ.Security.RestrictedSqlGate.VerifyAsync(builder.Configuration,
        ("DefaultConnection", "Auth"), ("OriginalLoginIssuer", "Issuer"));
var companyName = builder.Configuration["Company:BrandName"]!;

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection("Company"));
builder.Services.AddScoped<IAccountApiDbService, AccountApiDbService>();
if (builder.Configuration.GetValue<bool>("OriginalLogin:AuthorityEnabled"))
    builder.Services.AddScoped<IOriginalLoginIssuer, OriginalLoginIssuer>();
builder.Services.AddSingleton<IPasswordHashService, PasswordHashService>();
builder.Services.AddScoped<ProductsApiDbService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IContactNotificationSender, ContactNotificationSender>();
builder.Services.AddScoped<IInvoiceNotificationStore, InvoiceNotificationStore>();
builder.Services.AddScoped<IInvoiceNotificationSender, InvoiceNotificationSender>();
builder.Services.AddScoped<IDeliveryNotificationStore, DeliveryNotificationStore>();
builder.Services.AddScoped<IDeliveryNotificationSender, DeliveryNotificationSender>();
builder.Services.AddScoped<IRouteDispatchNotificationStore, RouteDispatchNotificationStore>();
builder.Services.AddScoped<IRouteDispatchNotificationSender, RouteDispatchNotificationSender>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICommercialQuoteAuthority, CommercialQuoteAuthority>();
builder.Services.AddSingleton<LoginAttemptLimiter>();
builder.Services.AddSingleton<PasswordRecoveryAttemptLimiter>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("contact-notification", limits =>
    {
        limits.PermitLimit = 5;
        limits.Window = TimeSpan.FromMinutes(1);
        limits.QueueLimit = 0;
    });
    options.AddConcurrencyLimiter("commercial-authority", limits =>
    {
        limits.PermitLimit = 4;
        limits.QueueLimit = 0;
    });
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
});

JJ.Runtime.LocalRuntimeControl.Register(builder.Services, builder.Configuration, builder.Environment);
var app = builder.Build();

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.DocumentTitle = $"{companyName} API";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
{
    service = "Proyecto_FinalAPI",
    project = companyName,
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
