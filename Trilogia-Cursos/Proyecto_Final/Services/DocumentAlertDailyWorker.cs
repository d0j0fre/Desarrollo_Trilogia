namespace Proyecto_Final.Services;

/// <summary>
/// Verificación diaria automática de vencimientos documentales (CU-202 / BUG-20-01).
/// Ejecuta la misma generación idempotente del botón "Generar sin duplicar": al iniciar
/// la aplicación y luego una vez por día hábil a partir de la hora configurada
/// (hora de Costa Rica). Entre revisiones no consulta la base de datos.
/// </summary>
public sealed class DocumentAlertDailyWorker : BackgroundService
{
    /// <summary>Usuario registrado como generador de las alertas automáticas.</summary>
    public const int SystemUserId = 0;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DocumentAlertDailyWorker> _logger;

    public DocumentAlertDailyWorker(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<DocumentAlertDailyWorker> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// True si todavía no se ejecutó en el día hábil actual y, salvo la primera ejecución
    /// tras iniciar, ya se alcanzó la hora diaria configurada.
    /// </summary>
    public static bool ShouldRun(DateTime? lastRunDate, DateTime businessNow, int dailyRunHour)
    {
        if (lastRunDate is null) return true;
        if (lastRunDate.Value.Date >= businessNow.Date) return false;
        return businessNow.Hour >= Math.Clamp(dailyRunHour, 0, 23);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("DocumentAlerts:AutomaticEnabled", true))
        {
            _logger.LogInformation("La verificación automática de alertas documentales está deshabilitada.");
            return;
        }

        var runHour = _configuration.GetValue("DocumentAlerts:DailyRunHour", 6);
        DateTime? lastRunDate = null;

        try
        {
            // Deja terminar el arranque de la aplicación antes de la primera verificación.
            await Task.Delay(TimeSpan.FromSeconds(30), _timeProvider, stoppingToken);

            using var timer = new PeriodicTimer(CheckInterval, _timeProvider);
            do
            {
                var businessNow = DocumentExpirationPolicy.BusinessDateTime(_timeProvider.GetUtcNow());
                if (ShouldRun(lastRunDate, businessNow, runHour) && await RunOnceAsync(stoppingToken))
                    lastRunDate = businessNow.Date;
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal de la aplicación.
        }
    }

    private async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var alerts = scope.ServiceProvider.GetRequiredService<IDocumentAlertService>();
            var count = await alerts.GenerateAsync(SystemUserId, cancellationToken);
            _logger.LogInformation("Verificación diaria de vencimientos documentales: {Count} alerta(s) nueva(s) o pendiente(s) de notificación.", count);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Un fallo (por ejemplo, base de datos no disponible) no detiene la aplicación;
            // se reintenta en la siguiente revisión.
            _logger.LogError(exception, "Falló la verificación diaria de vencimientos documentales.");
            return false;
        }
    }
}
