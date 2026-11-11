namespace Proyecto_Final.Services;

/// <summary>
/// Explica por qué no se puede notificar una boleta cuando falta configuración (G2-27), en vez del
/// mensaje genérico «no fue posible confirmar el envío».
/// </summary>
public static class PaySlipDeliveryDiagnostics
{
    public const string MissingBaseUrlMessage =
        "No se pudo notificar la boleta: falta configurar la URL pública del sitio (PaySlips:PublicBaseUrl, una dirección HTTPS). " +
        "La boleta permanece disponible en el portal; pida a quien administra el sitio que configure ese valor.";

    public const string MissingMailMessage =
        "No se pudo notificar la boleta: el correo del sistema no está configurado (ConfiguracionCorreo: Host, Puerto, Remitente y Contrasenna). " +
        "La boleta permanece disponible en el portal.";

    /// <summary>Devuelve el motivo en lenguaje claro, o null si la configuración permite enviar.</summary>
    public static string? GetConfigurationProblem(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!Uri.TryCreate(configuration["PaySlips:PublicBaseUrl"]?.Trim(), UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
            return MissingBaseUrlMessage;

        if (string.IsNullOrWhiteSpace(configuration["ConfiguracionCorreo:Host"])
            || !int.TryParse(configuration["ConfiguracionCorreo:Puerto"], out var port) || port <= 0
            || string.IsNullOrWhiteSpace(configuration["ConfiguracionCorreo:Remitente"])
            || string.IsNullOrWhiteSpace(configuration["ConfiguracionCorreo:Contrasenna"]))
            return MissingMailMessage;

        return null;
    }
}
