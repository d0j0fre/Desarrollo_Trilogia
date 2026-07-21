using Proyecto_Final.Middleware;
using Proyecto_Final.Services;
using Proyecto_Final.Hubs;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(45);
    options.Cookie.Name = ".DistribuidoraJJ.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
builder.Services.AddScoped<AssistantService>();

// HttpClient para consumir la API de autenticación
builder.Services.AddHttpClient<AccountApiService>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("No se encontró la configuración ApiSettings:BaseUrl en appsettings.json.");
    }
    Console.WriteLine(baseUrl);
    client.BaseAddress = new Uri(baseUrl);
    //client.BaseAddress = new Uri(baseUrl);
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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub");

app.Run();
