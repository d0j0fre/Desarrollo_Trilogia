using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

/// <summary>Documento de flota que alcanzó un umbral de aviso (30, 15, 7, 1 o 0 días) y aún no se avisó por correo.</summary>
public sealed record FleetDocumentNotice(int DocumentoId, string VehiculoPlaca, string Tipo, DateTime FechaVencimiento, int DiasRestantes, int UmbralDias);

/// <summary>Persona de Administración o Logística que recibe los avisos de flota.</summary>
public sealed record FleetAlertRecipient(int UsuarioId, string Nombre, string Correo);

/// <summary>Resultado de un envío de avisos de flota, para informarlo en pantalla.</summary>
public sealed record FleetAlertNotificationResult(bool Enabled, bool SmtpConfigured, int Documents, int Sent, int Failed, int WithoutRecipients)
{
    public static FleetAlertNotificationResult Disabled { get; } = new(false, true, 0, 0, 0, 0);
    public static FleetAlertNotificationResult NotConfigured { get; } = new(true, false, 0, 0, 0, 0);

    /// <summary>Mensaje para el usuario que presionó el botón o registró el documento.</summary>
    public string Describe()
    {
        if (!Enabled) return "El envío de avisos por correo está desactivado (DocumentAlerts:EmailEnabled). Las alertas se muestran en pantalla.";
        if (!SmtpConfigured) return "No se enviaron correos: la configuración del correo del sistema está incompleta (ConfiguracionCorreo).";
        if (Documents == 0) return "No hay avisos pendientes: todos los documentos por vencer ya se avisaron por correo.";
        if (WithoutRecipients > 0 && Sent == 0) return "No se envió ningún correo: no hay usuarios de Administración o Logística con correo registrado.";
        if (Failed > 0 && Sent == 0) return "No fue posible enviar los correos de aviso de flota. Se reintentará en la próxima verificación.";
        return Failed > 0
            ? $"Se avisó por correo de {Sent} documento(s) de flota; {Failed} no se pudo enviar y se reintentará."
            : $"Se avisó por correo a Administración y Logística de {Sent} documento(s) de flota por vencer.";
    }
}

public interface IFleetAlertNotifier
{
    /// <param name="extraThresholds">Umbrales adicionales a los configurados (por ejemplo, los días de aviso elegidos en pantalla).</param>
    Task<FleetAlertNotificationResult> NotifyAsync(IEnumerable<int>? extraThresholds, CancellationToken cancellationToken = default);
}

/// <summary>
/// CU-154-02: avisa por correo a Administración y Logística cuando un documento legal de la flota
/// (Marchamo, Seguro, Revisión técnica…) alcanza un umbral de días antes de vencer. Idempotente:
/// cada documento y umbral se avisa una sola vez (tabla VehiculoDocumentoAvisos, migración 0335);
/// un envío fallido se reintenta en la siguiente verificación. Usa los mismos interruptores que las
/// alertas documentales: <c>DocumentAlerts:EmailEnabled</c> y <c>DocumentAlerts:DefaultWarningDays</c>.
/// </summary>
public sealed class FleetAlertNotifier : IFleetAlertNotifier
{
    private static readonly int[] FallbackThresholds = [30, 15, 7, 1, 0];
    private readonly FleetDbService _fleet;
    private readonly EmailService _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FleetAlertNotifier> _logger;

    public FleetAlertNotifier(FleetDbService fleet, EmailService email, IConfiguration configuration, ILogger<FleetAlertNotifier> logger)
    {
        _fleet = fleet;
        _email = email;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<FleetAlertNotificationResult> NotifyAsync(IEnumerable<int>? extraThresholds, CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue("DocumentAlerts:EmailEnabled", false)) return FleetAlertNotificationResult.Disabled;
        if (!SmtpConfigured())
        {
            _logger.LogWarning("DocumentAlerts:EmailEnabled está activo pero ConfiguracionCorreo está incompleta: no se envían los avisos de flota.");
            return FleetAlertNotificationResult.NotConfigured;
        }

        var thresholds = Thresholds().Concat(extraThresholds ?? []).Where(x => x is >= 0 and <= 365).Distinct().OrderBy(x => x).ToArray();
        var (notices, recipients) = await _fleet.GetPendingDocumentNoticesAsync(thresholds);
        if (notices.Count == 0) return new FleetAlertNotificationResult(true, true, 0, 0, 0, 0);

        if (recipients.Count == 0)
        {
            // No se registra el aviso: en cuanto exista un destinatario se enviará.
            _logger.LogWarning("Hay {Count} documento(s) de flota por vencer pero ningún usuario de Administración o Logística con correo.", notices.Count);
            return new FleetAlertNotificationResult(true, true, notices.Count, 0, 0, notices.Count);
        }

        var link = PublicSiteUrl.Build(_configuration, "Fleet/Alerts");
        var sent = 0;
        var failed = 0;
        foreach (var notice in notices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subject = $"Documento de flota por vencer: {notice.Tipo} · {notice.VehiculoPlaca}";
            var body = EmailTemplateBuilder.BuildFleetDocumentAlertEmail(notice.VehiculoPlaca, notice.Tipo, notice.FechaVencimiento, notice.DiasRestantes, link);

            var delivered = 0;
            string? error = null;
            foreach (var recipient in recipients)
            {
                try
                {
                    _email.SendEmail(recipient.Correo, subject, body);
                    delivered++;
                }
                catch (Exception exception)
                {
                    error = exception.GetType().Name;
                    _logger.LogWarning(exception, "No se pudo avisar a {UsuarioId} del documento de flota {DocumentoId}.", recipient.UsuarioId, notice.DocumentoId);
                }
            }

            if (delivered > 0) sent++; else failed++;
            try
            {
                await _fleet.RegisterDocumentNoticeAsync(notice.DocumentoId, notice.UmbralDias, delivered > 0 ? "Enviado" : "Fallido", delivered, delivered > 0 ? null : error);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No se pudo registrar el aviso del documento de flota {DocumentoId}.", notice.DocumentoId);
            }
        }

        return new FleetAlertNotificationResult(true, true, notices.Count, sent, failed, 0);
    }

    private int[] Thresholds()
    {
        var values = _configuration.GetSection("DocumentAlerts:DefaultWarningDays").Get<int[]>();
        return values is { Length: > 0 } ? values : FallbackThresholds;
    }

    private bool SmtpConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["ConfiguracionCorreo:Host"]) &&
        _configuration.GetValue<int>("ConfiguracionCorreo:Puerto") > 0 &&
        !string.IsNullOrWhiteSpace(_configuration["ConfiguracionCorreo:Remitente"]) &&
        !string.IsNullOrWhiteSpace(_configuration["ConfiguracionCorreo:Contrasenna"]);
}
