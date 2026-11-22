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

/// <summary>M-03: resultado de «Generar sin duplicar»: alertas candidatas y qué pasó con el correo.</summary>
public sealed record DocumentAlertRunResult(int Candidates, int EmailsSent, int EmailsFailed, bool EmailDisabled, bool SmtpIncomplete)
{
    /// <summary>Frase para la pantalla: cuántos correos salieron, o por qué no salió ninguno.</summary>
    public string Describe()
    {
        var text = $"Proceso completado: {Candidates} alerta(s) nueva(s) o pendiente(s) de notificación";
        if (EmailDisabled) return text + "; no se enviaron correos porque el envío de correos está apagado.";
        if (SmtpIncomplete) return text + "; no se enviaron correos porque la configuración del correo está incompleta.";
        if (EmailsSent == 0 && EmailsFailed == 0) return text + "; no se enviaron correos (no había destinatarios pendientes).";
        var enviados = EmailsSent == 1 ? "se envió 1 correo" : $"se enviaron {EmailsSent} correos";
        return EmailsFailed > 0
            ? $"{text}; {enviados} y {EmailsFailed} no se pudieron enviar (se reintentarán)."
            : $"{text}, y {enviados}.";
    }
}

public interface IDocumentAlertService
{
    DateTime CurrentBusinessDate();
    int DefaultWarningDays { get; }
    Task<int> GenerateAsync(int userId, CancellationToken cancellationToken = default);
    Task<DocumentAlertRunResult> GenerateDetailedAsync(int userId, CancellationToken cancellationToken = default);
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

    public async Task<int> GenerateAsync(int userId, CancellationToken cancellationToken = default) =>
        (await GenerateDetailedAsync(userId, cancellationToken)).Candidates;

    public async Task<DocumentAlertRunResult> GenerateDetailedAsync(int userId, CancellationToken cancellationToken = default)
    {
        var businessDate = CurrentBusinessDate();
        var candidates = (await _db.GenerateAlertsAsync(businessDate, Thresholds(), userId)).ToList();

        if (!EmailEnabled)
        {
            return new DocumentAlertRunResult(candidates.Count, 0, 0, EmailDisabled: true, SmtpIncomplete: false);
        }

        if (!SmtpConfigured())
        {
            _logger.LogWarning("DocumentAlerts:EmailEnabled está activo pero ConfiguracionCorreo está incompleta (Host, Puerto, Remitente, Contrasenna): no se envían los correos de alertas documentales.");
            return new DocumentAlertRunResult(candidates.Count, 0, 0, EmailDisabled: false, SmtpIncomplete: true);
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
        int sent = 0, failed = 0;
        foreach (var candidate in candidates.Where(item => !string.IsNullOrWhiteSpace(item.Recipient)))
        {
            var delivered = false;
            try
            {
                var days = (int)(candidate.ExpirationDate.Date - businessDate.Date).TotalDays;
                var body = EmailTemplateBuilder.BuildDocumentAlertEmail(candidate.DocumentTitle, candidate.ExpirationDate, days, link);
                await _email.SendAsync(candidate.Recipient, $"Alerta de vencimiento: {candidate.DocumentTitle}", body, cancellationToken);
                delivered = true;
                sent++;
                await _db.RegisterNotificationAsync(candidate, "Enviado", null);
            }
            catch (Exception exception)
            {
                // Si el correo salió y solo falló el registro, ya cuenta como enviado: no se cuenta además como fallido.
                if (!delivered) failed++;
                _logger.LogWarning(exception, "No se pudo enviar la alerta {AlertId} del documento {DocumentId}.", candidate.AlertId, candidate.DocumentId);
                if (!delivered)
                {
                    try { await _db.RegisterNotificationAsync(candidate, "Fallido", exception.GetType().Name); }
                    catch (Exception registerFailure) { _logger.LogWarning(registerFailure, "No se pudo registrar el fallo de la alerta {AlertId}.", candidate.AlertId); }
                }
            }
        }
        return new DocumentAlertRunResult(candidates.Count, sent, failed, EmailDisabled: false, SmtpIncomplete: false);
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
