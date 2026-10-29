using Proyecto_Final.Services.ChatV2;
using Proyecto_Final.Services.ChatV2.Broker;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_Final.Middleware;
using Proyecto_Final.Services;
using Proyecto_Final.Hubs;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CommercialPricingAuthority")))
    throw new InvalidOperationException("MVC no puede contener la identidad SQL de precios.");
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("InvoiceNotificationAuthority")))
    throw new InvalidOperationException("MVC no puede contener la identidad SQL de notificaciones.");
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DeliveryNotificationAuthority")))
    throw new InvalidOperationException("MVC no puede contener la autoridad SQL de avisos logísticos.");
if (!string.IsNullOrWhiteSpace(builder.Configuration["RouteDispatchNotifications:ConnectionString"]))
    throw new InvalidOperationException("MVC no puede contener la autoridad SQL de avisos de despacho.");
StartupConfigurationValidator.Validate(builder.Configuration, builder.Environment);
ChatV2SqlConfiguration.Validate(builder.Configuration);
if (builder.Configuration.GetValue<bool>("ChatV2:PersonalSqlEnabled"))
{
    bool broker = builder.Configuration.GetValue<bool>("ChatV2:BrokerEnabled");
    if (broker && !OperatingSystem.IsWindows()) throw new InvalidOperationException("El broker local requiere Windows.");
    await DistribuidoraJJ.Security.RestrictedSqlGate.VerifyAsync(builder.Configuration,
        (broker ? "DefaultConnection" : "ChatV2App", broker ? "MvcApp" : "App"),
        ("ChatV2Reader", broker ? "ArchiveReader" : "Reader"));
}

// MVC
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ICommercialOrderService, CommercialOrderService>();
builder.Services.AddScoped<IClientBillingSummaryService, ClientBillingSummaryService>();
builder.Services.AddScoped<IDocumentDelegationService, DocumentDelegationService>();
if (builder.Configuration.GetValue<bool>("Documents:DelegationEnabled"))
    await DocumentDelegationManifest.VerifyAsync(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Falta la identidad APP documental."));
if (builder.Configuration.GetValue<bool>("Commerce:OrdersEnabled"))
{
    if (!builder.Configuration.GetValue<bool>("Commerce:Enabled"))
        throw new InvalidOperationException("Los pedidos requieren la autoridad comercial verificada.");
    await CommercialOrderManifest.VerifyAsync(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Falta la identidad APP comercial."));
    await ClientBillingSummaryManifest.VerifyAsync(builder.Configuration.GetConnectionString("DefaultConnection")!);
}
if (builder.Configuration.GetValue<bool>("Commerce:Enabled"))
{
builder.Services.AddSingleton<CommercialClientCertificate>();
builder.Services.AddHttpClient<ICommercialQuoteClient, CommercialQuoteClient>((services, client) =>
{
    var settings = services.GetRequiredService<IConfiguration>();
    var uri = new Uri(settings["ApiSettings:BaseUrl"] ?? throw new InvalidOperationException("Falta el destino API."));
    if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsLoopback) throw new InvalidOperationException("La autoridad local de precios requiere HTTPS de loopback.");
    client.BaseAddress = uri;
    client.Timeout = TimeSpan.FromSeconds(18);
}).ConfigurePrimaryHttpMessageHandler(services =>
{
    var handler = new HttpClientHandler { AllowAutoRedirect = false };
    handler.ClientCertificates.Add(services.GetRequiredService<CommercialClientCertificate>().Certificate);
    return handler;
});
}
else builder.Services.AddScoped<ICommercialQuoteClient, DisabledCommercialQuoteClient>();
if (builder.Configuration.GetValue<bool>("Contact:NotificationsEnabled"))
{
    if (!builder.Configuration.GetValue<bool>("Commerce:Enabled"))
        throw new InvalidOperationException("Las notificaciones requieren la identidad TLS local acreditada.");
    builder.Services.AddHttpClient<IContactNotificationClient, ContactNotificationClient>(client =>
    {
        if (!Uri.TryCreate(builder.Configuration["ApiSettings:BaseUrl"], UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsLoopback)
            throw new InvalidOperationException("Las notificaciones requieren HTTPS local.");
        client.BaseAddress = uri;
        client.Timeout = TimeSpan.FromSeconds(18);
    }).ConfigurePrimaryHttpMessageHandler(services =>
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        handler.ClientCertificates.Add(services.GetRequiredService<CommercialClientCertificate>().Certificate);
        return handler;
    });
}
else builder.Services.AddScoped<IContactNotificationClient, DisabledContactNotificationClient>();
if (builder.Configuration.GetValue<bool>("InvoiceNotifications:Enabled"))
{
    if (!builder.Configuration.GetValue<bool>("Commerce:Enabled"))
        throw new InvalidOperationException("Las notificaciones de factura requieren mTLS local.");
    builder.Services.AddHttpClient<IInvoiceNotificationClient, InvoiceNotificationClient>(client =>
    {
        if (!Uri.TryCreate(builder.Configuration["ApiSettings:BaseUrl"], UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsLoopback)
            throw new InvalidOperationException("Las notificaciones de factura requieren HTTPS local.");
        client.BaseAddress = uri;
        client.Timeout = TimeSpan.FromSeconds(18);
    }).ConfigurePrimaryHttpMessageHandler(services =>
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        handler.ClientCertificates.Add(services.GetRequiredService<CommercialClientCertificate>().Certificate);
        return handler;
    });
}
else builder.Services.AddScoped<IInvoiceNotificationClient, DisabledInvoiceNotificationClient>();
if (builder.Configuration.GetValue<bool>("DeliveryNotifications:Enabled"))
{
    if (!builder.Configuration.GetValue<bool>("Commerce:Enabled")) throw new InvalidOperationException("Los avisos logísticos requieren mTLS local.");
    builder.Services.AddHttpClient<IDeliveryNotificationClient, DeliveryNotificationClient>(client =>
    {
        if (!Uri.TryCreate(builder.Configuration["ApiSettings:BaseUrl"], UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsLoopback) throw new InvalidOperationException("Los avisos logísticos requieren HTTPS local.");
        client.BaseAddress = uri;
        client.Timeout = TimeSpan.FromSeconds(18);
    }).ConfigurePrimaryHttpMessageHandler(services =>
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        handler.ClientCertificates.Add(services.GetRequiredService<CommercialClientCertificate>().Certificate);
        return handler;
    });
}
else builder.Services.AddScoped<IDeliveryNotificationClient, DisabledDeliveryNotificationClient>();
if (builder.Configuration.GetValue<bool>("RouteDispatchNotifications:Enabled"))
{
    if (!builder.Configuration.GetValue<bool>("Commerce:Enabled")) throw new InvalidOperationException("Los avisos de despacho requieren mTLS local.");
    builder.Services.AddHttpClient<IRouteDispatchNotificationClient, RouteDispatchNotificationClient>(client =>
    {
        if (!Uri.TryCreate(builder.Configuration["ApiSettings:BaseUrl"], UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsLoopback) throw new InvalidOperationException("Los avisos de despacho requieren HTTPS local.");
        client.BaseAddress = uri;
        client.Timeout = TimeSpan.FromSeconds(18);
    }).ConfigurePrimaryHttpMessageHandler(services =>
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        handler.ClientCertificates.Add(services.GetRequiredService<CommercialClientCertificate>().Certificate);
        return handler;
    });
}
else builder.Services.AddScoped<IRouteDispatchNotificationClient, DisabledRouteDispatchNotificationClient>();
if (!string.IsNullOrWhiteSpace(builder.Configuration["DataProtection:KeysPath"]))
{
    var keys = builder.Services.AddDataProtection().PersistKeysToFileSystem(
        new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"]!));
    if (OperatingSystem.IsWindows()) keys.ProtectKeysWithDpapi();
}
builder.Services.AddSignalR();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
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

    options.AddPolicy("public-contact", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("chat-send", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(
            GetUserPartition(httpContext),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 30,
                TokensPerPeriod = 30,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("chat-search", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("assistant", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("evidence-upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("private-file-upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("document-alert-generation", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("route-dispatch-notification", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("delivery-notification", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("finance-write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("sensitive-read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetUserPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Session compartida: memoria solo para desarrollo local; Redis es obligatorio en producción.
if (builder.Environment.IsProduction())
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("DistributedCache");
        options.InstanceName = "DistribuidoraJJ:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(45);
    options.Cookie.Name = ".DistribuidoraJJ.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Servicios propios
builder.Services.AddScoped<AdminDbService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IEmployeeRelationshipService, EmployeeRelationshipService>();
builder.Services.AddScoped<IWorkspaceResolver, WorkspaceResolver>();
builder.Services.AddScoped<IUserSessionValidationService, UserSessionValidationService>();
builder.Services.AddScoped<IOriginalLoginService, OriginalLoginService>();
builder.Services.AddSingleton<IPasswordHashService, PasswordHashService>();
builder.Services.AddScoped<IEmployeesService, EmployeesDbService>();
builder.Services.AddScoped<IAttendanceService, AttendanceDbService>();
builder.Services.AddScoped<IPayrollService, PayrollDbService>();
builder.Services.AddScoped<IPaySlipService, PaySlipDbService>();
builder.Services.AddScoped<IPaySlipEmailSender, SmtpPaySlipEmailSender>();
builder.Services.AddScoped<IPaySlipDeliveryCoordinator, PaySlipDeliveryCoordinator>();
builder.Services.AddScoped<StoreDbService>();
builder.Services.AddScoped<AccountDbService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<LogisticsDbService>();
builder.Services.AddScoped<ReportsDbService>();
builder.Services.AddScoped<WarehouseDbService>();
builder.Services.AddScoped<FleetDbService>();
builder.Services.AddScoped<PromotionsDbService>();
builder.Services.AddScoped<ReclamosDbService>();
builder.Services.AddScoped<KpiDbService>();
builder.Services.AddScoped<ExpensesDbService>();
builder.Services.AddScoped<BudgetDbService>();
builder.Services.AddScoped<BudgetComparisonDbService>();
builder.Services.AddScoped<IComboDbService, ComboDbService>();
builder.Services.AddScoped<IInventoryTransformationService, InventoryTransformationDbService>();
builder.Services.AddScoped<IInventoryIntelligenceService, InventoryIntelligenceDbService>();
builder.Services.AddScoped<IPurchasingService, PurchasingDbService>();
builder.Services.AddScoped<ICrossSellService, CrossSellDbService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IDocumentManagementDbService, DocumentManagementDbService>();
builder.Services.AddScoped<IDocumentAlertService, DocumentAlertService>();
builder.Services.AddScoped<IDocumentAlertEmailSender, SmtpDocumentAlertEmailSender>();
builder.Services.AddScoped<AssistantService>();
builder.Services.AddScoped<IChatDbService, ChatDbService>();
builder.Services.AddScoped<IChatAuthorizationService, ChatAuthorizationService>();
builder.Services.AddScoped<LegacyChatWriteGuard>();
builder.Services.AddScoped<ICommercialRelationsService, CommercialRelationsService>();
builder.Services.AddScoped<SqlLegacyPersonalArchive>();
builder.Services.AddChatV2Continuity();
if (builder.Configuration.GetValue<bool>("ChatV2:PersonalSignalREnabled")) builder.Services.AddPersonalChatV2Transport();
// Explicit opt-in: normal installations remain closed until their durable SQL contract is accredited.
if (builder.Configuration.GetValue<bool>("ChatV2:PersonalSqlEnabled"))
{
    if (builder.Configuration.GetValue<bool>("ChatV2:BrokerEnabled"))
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("El broker local requiere Windows.");
        var settings = new BrokerPipeSettings(builder.Configuration["ChatV2:BrokerPipe"]!,
            builder.Configuration["ChatV2:BrokerWindowsSid"]!, builder.Configuration["ChatV2:AppWindowsSid"]!);
        settings.Validate();
        builder.Services.AddSingleton<IChatV2BrokerExchange>(new ChatV2BrokerPipeClient(settings));
        builder.Services.AddScoped<IChatV2IntentPersistence, BrokerChatV2Persistence>();
    }
    else builder.Services.AddScoped<IChatV2IntentPersistence, SqlPersonalChatV2Persistence>();
}
builder.Services.AddSingleton<IEvidenceStorageService, FileEvidenceStorageService>();
builder.Services.AddSingleton<IPrivateFileStorageService, PrivateFileStorageService>();
builder.Services.AddSingleton<IProductImageStorageService, ProductImageStorageService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BusinessClock>();
builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection("Company"));

// HttpClient para consumir la API de autenticación
builder.Services.AddHttpClient<AccountApiService>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("No se encontró la configuración ApiSettings:BaseUrl en appsettings.json.");
    }
    client.BaseAddress = new Uri(baseUrl);
});

JJ.Runtime.LocalRuntimeControl.Register(builder.Services, builder.Configuration, builder.Environment);
var app = builder.Build();
if (app.Configuration.GetValue<bool>("Commerce:Enabled"))
    _ = app.Services.GetRequiredService<CommercialClientCertificate>();

// Valida la ruta y crea la estructura privada al iniciar, no en la primera carga.
_ = app.Services.GetRequiredService<IPrivateFileStorageService>();

// Pipeline
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseMiddleware<SessionValidationMiddleware>();
// Explicit order prevents WebApplication's automatic authentication from running
// and caching a denial before the original session has been validated.
if (app.Configuration.GetValue<bool>("ChatV2:PersonalSignalREnabled")) app.UseAuthentication();
app.UseRateLimiter();

app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "OK",
    service = "Proyecto_Final",
    build = BuildIdentity.CommitSha
}));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// The legacy transport is not a fallback for the durable personal adapter.
if (app.Configuration.GetValue<bool>("ChatV2:PersonalSignalREnabled"))
    app.MapHub<PersonalChatV2Hub>(PersonalChatV2TransportIntegration.Path, options =>
        options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets);
// V2 SignalR/broker remain closed until their independent gate is accredited.
if (!builder.Configuration.GetValue<bool>("ChatV2:PersonalSqlEnabled")
    && !builder.Configuration.GetValue<bool>("ChatV2:LegacyReadOnly"))
    app.MapHub<ChatHub>("/chatHub");

app.Run();

static string GetUserPartition(HttpContext httpContext)
{
    var userId = httpContext.Session.GetInt32("UserId");
    return userId.HasValue && userId.Value > 0
        ? $"user:{userId.Value}"
        : $"ip:{httpContext.Connection.RemoteIpAddress}";
}
