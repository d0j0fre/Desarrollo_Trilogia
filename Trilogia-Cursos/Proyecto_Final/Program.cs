using Microsoft.AspNetCore.RateLimiting;
using Proyecto_Final.Middleware;
using Proyecto_Final.Services;
using Proyecto_Final.Hubs;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();
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

// Session
builder.Services.AddDistributedMemoryCache();
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
builder.Services.AddScoped<EmployeesDbService>();
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
builder.Services.AddScoped<IDocumentManagementDbService, DocumentManagementDbService>();
builder.Services.AddScoped<IDocumentAlertService, DocumentAlertService>();
builder.Services.AddScoped<IDocumentAlertEmailSender, SmtpDocumentAlertEmailSender>();
builder.Services.AddScoped<AssistantService>();
builder.Services.AddScoped<IChatDbService, ChatDbService>();
builder.Services.AddScoped<IChatAuthorizationService, ChatAuthorizationService>();
builder.Services.AddSingleton<IEvidenceStorageService, FileEvidenceStorageService>();
builder.Services.AddSingleton<IPrivateFileStorageService, PrivateFileStorageService>();
builder.Services.AddSingleton(TimeProvider.System);

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

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub");

app.Run();

static string GetUserPartition(HttpContext httpContext)
{
    var userId = httpContext.Session.GetInt32("UserId");
    return userId.HasValue && userId.Value > 0
        ? $"user:{userId.Value}"
        : $"ip:{httpContext.Connection.RemoteIpAddress}";
}
