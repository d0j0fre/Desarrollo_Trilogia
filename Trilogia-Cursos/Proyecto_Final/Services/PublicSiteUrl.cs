namespace Proyecto_Final.Services;

/// <summary>
/// Dirección pública del sitio para armar enlaces dentro de los correos (nunca localhost en Azure).
/// Es la misma variable que ya usan las boletas de pago: <c>PaySlips:PublicBaseUrl</c> (en Azure
/// <c>PaySlips__PublicBaseUrl</c>). Debe ser una dirección HTTPS absoluta; si falta o no es válida,
/// los correos se envían igual pero sin botón de enlace (nunca con un enlace roto).
/// </summary>
public static class PublicSiteUrl
{
    public const string ConfigurationKey = "PaySlips:PublicBaseUrl";

    public static Uri? TryGetBase(IConfiguration? configuration)
    {
        var raw = configuration?[ConfigurationKey]?.Trim();
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var baseUri)) return null;
        return baseUri.Scheme == Uri.UriSchemeHttps ? baseUri : null;
    }

    /// <summary>Enlace absoluto a una ruta del sitio (por ejemplo "ClientPortal/Detail/5") o null si no hay URL pública.</summary>
    public static string? Build(IConfiguration? configuration, string relativePath)
    {
        var baseUri = TryGetBase(configuration);
        if (baseUri is null) return null;
        var root = baseUri.AbsoluteUri.TrimEnd('/') + "/";
        return new Uri(new Uri(root), relativePath.TrimStart('/')).AbsoluteUri;
    }
}
