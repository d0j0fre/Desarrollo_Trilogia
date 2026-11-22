using System.Net;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

public interface IDocumentAlertEmailSender
{
    Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken);
}

public sealed class SmtpDocumentAlertEmailSender : IDocumentAlertEmailSender
{
    private readonly EmailService _email;
    public SmtpDocumentAlertEmailSender(EmailService email) => _email = email;

    public Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _email.SendEmail(recipient, subject, html);
        return Task.CompletedTask;
    }
}

public interface IDocumentAlertService
{
    DateTime CurrentBusinessDate();
    int DefaultWarningDays { get; }
    Task<int> GenerateAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed class DocumentAlertService : IDocumentAlertService
{
    private static readonly int[] FallbackThresholds = [30, 15, 7, 1, 0];
    private readonly IDocumentManagementDbService _db;
    private readonly IDocumentAlertEmailSender _email;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DocumentAlertService> _logger;

    public DocumentAlertService(
        IDocumentManagementDbService db,
        IDocumentAlertEmailSender email,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<DocumentAlertService> logger)
    {
        _db = db;
        _email = email;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public DateTime CurrentBusinessDate() => DocumentExpirationPolicy.BusinessDate(_timeProvider.GetUtcNow());

    public int DefaultWarningDays => Thresholds().DefaultIfEmpty(30).Max();

    public async Task<int> GenerateAsync(int userId, CancellationToken cancellationToken = default)
    {
        var businessDate = CurrentBusinessDate();
        var candidates = (await _db.GenerateAlertsAsync(businessDate, Thresholds(), userId)).ToList();

        if (!EmailEnabled)
        {
            return candidates.Count;
        }

        if (!SmtpConfigured())
        {
            _logger.LogWarning("DocumentAlerts:EmailEnabled está activo pero ConfiguracionCorreo está incompleta (Host, Puerto, Remitente, Contrasenna): no se envían los correos de alertas documentales.");
            return candidates.Count;
        }

        // Alertas que ya existían pero nunca se avisaron por correo (por ejemplo, creadas antes de configurar el SMTP).
        try
        {
            var pending = await _db.GetPendingNotificationsAsync() ?? [];
            var known = candidates.Select(item => item.AlertId).ToHashSet();
            candidates.AddRange(pending.Where(item => known.Add(item.AlertId)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Base sin la migración 0335: se siguen enviando las alertas nuevas.
            _logger.LogWarning(exception, "No se pudieron consultar las alertas documentales pendientes de correo (¿falta la migración 0335?).");
        }

        var link = PublicSiteUrl.Build(_configuration, "Documents/Alerts");
        foreach (var candidate in candidates.Where(item => !string.IsNullOrWhiteSpace(item.Recipient)))
        {
            try
            {
                var days = (int)(candidate.ExpirationDate.Date - businessDate.Date).TotalDays;
                var body = EmailTemplateBuilder.BuildDocumentAlertEmail(candidate.DocumentTitle, candidate.ExpirationDate, days, link);
                await _email.SendAsync(candidate.Recipient, $"Alerta de vencimiento: {candidate.DocumentTitle}", body, cancellationToken);
                await _db.RegisterNotificationAsync(candidate, "Enviado", null);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No se pudo enviar la alerta {AlertId} del documento {DocumentId}.", candidate.AlertId, candidate.DocumentId);
                try { await _db.RegisterNotificationAsync(candidate, "Fallido", exception.GetType().Name); }
                catch (Exception registerFailure) { _logger.LogWarning(registerFailure, "No se pudo registrar el fallo de la alerta {AlertId}.", candidate.AlertId); }
            }
        }
        return candidates.Count;
    }

    private bool EmailEnabled => _configuration.GetValue("DocumentAlerts:EmailEnabled", false);

    private int[] Thresholds()
    {
        var values = _configuration.GetSection("DocumentAlerts:DefaultWarningDays").Get<int[]>();
        return (values is { Length: > 0 } ? values : FallbackThresholds)
            .Where(value => value is >= 0 and <= 365).Distinct().OrderBy(value => value).ToArray();
    }

    private bool SmtpConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["ConfiguracionCorreo:Host"]) &&
        _configuration.GetValue<int>("ConfiguracionCorreo:Puerto") > 0 &&
        !string.IsNullOrWhiteSpace(_configuration["ConfiguracionCorreo:Remitente"]) &&
        !string.IsNullOrWhiteSpace(_configuration["ConfiguracionCorreo:Contrasenna"]);
}
