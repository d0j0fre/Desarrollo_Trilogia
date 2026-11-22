namespace Proyecto_Final.Services;

/// <summary>
/// Verificación diaria automática de facturas a crédito vencidas (POST-qa2-03 · CU-093-04, 0326).
/// Deja el aviso «Factura a crédito vencida» en la bandeja de Administrador, Gerente y Crédito y Cobro,
/// una vez por cliente y día (la generación es idempotente). Corre al iniciar la aplicación y luego una
/// vez por día a partir de la hora configurada (hora de Costa Rica); entre revisiones no consulta la base.
/// Mismo esquema que <see cref="DocumentAlertDailyWorker"/>.
/// </summary>
public sealed class InvoiceOverdueDailyWorker : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InvoiceOverdueDailyWorker> _logger;

    public InvoiceOverdueDailyWorker(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<InvoiceOverdueDailyWorker> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>True si todavía no corrió en el día actual y, salvo la primera corrida tras iniciar, ya se alcanzó la hora diaria.</summary>
    public static bool ShouldRun(DateTime? lastRunDate, DateTime businessNow, int dailyRunHour) =>
        DocumentAlertDailyWorker.ShouldRun(lastRunDate, businessNow, dailyRunHour);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("InvoiceOverdueAlerts:AutomaticEnabled", true))
        {
            _logger.LogInformation("La verificación automática de facturas vencidas está deshabilitada.");
            return;
        }

        var runHour = _configuration.GetValue("InvoiceOverdueAlerts:DailyRunHour", 6);
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
            var admin = scope.ServiceProvider.GetRequiredService<AdminDbService>();
            var count = await admin.GenerateOverdueInvoiceAlertsAsync(cancellationToken);
            _logger.LogInformation("Verificación diaria de facturas vencidas: {Count} aviso(s) nuevo(s).", count);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Un fallo (por ejemplo, base de datos no disponible o migración 0326 sin aplicar) no detiene
            // la aplicación; se reintenta en la siguiente revisión.
            _logger.LogError(exception, "Falló la verificación diaria de facturas vencidas.");
            return false;
        }
    }
}
