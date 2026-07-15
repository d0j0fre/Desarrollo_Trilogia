using Proyecto_FinalAPI.Middleware;
using Proyecto_FinalAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<AccountApiDbService>();
builder.Services.AddScoped<ProductsApiDbService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<LoginAttemptLimiter>();
builder.Services.AddSingleton<PasswordRecoveryAttemptLimiter>();

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
