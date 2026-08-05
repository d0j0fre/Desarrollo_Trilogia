using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Middleware;
using Proyecto_FinalAPI.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAccountApiDbService, AccountApiDbService>();
builder.Services.AddScoped<ProductsApiDbService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<LoginAttemptLimiter>();
builder.Services.AddSingleton<PasswordRecoveryAttemptLimiter>();
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
});

var app = builder.Build();

var swaggerEnabled = app.Environment.IsDevelopment() ||
                     app.Configuration.GetValue<bool>("Swagger:Enabled");

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
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();

app.MapGet("/", (IHostEnvironment environment) => Results.Ok(new
{
    service = "Proyecto_FinalAPI",
    project = "DistribuidoraJJ - Licorera La Bodega",
    status = "OK",
    environment = environment.EnvironmentName,
    swagger = "/swagger",
    health = "/health"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "OK",
    service = "Proyecto_FinalAPI"
}));

app.MapControllers();
app.Run();
