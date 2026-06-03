using Proyecto_Final.Services;
using Proyecto_FinalAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Servicios propios
builder.Services.AddScoped<AdminDbService>();
builder.Services.AddScoped<StoreDbService>();
builder.Services.AddScoped<AccountDbService>();
builder.Services.AddScoped<EmailService>();

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
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();