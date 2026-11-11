using System.Globalization;

namespace Proyecto_Final.Services;

/// <summary>
/// Formato de montos y fechas de Personal, Planilla y Boletas: no depende de la cultura del servidor.
/// Dinero «₡255 000,00» (grupos con espacio, coma decimal) y fecha «16/10/2026».
/// </summary>
public static class RrhhFormat
{
    private static readonly CultureInfo CostaRica = CultureInfo.GetCultureInfo("es-CR");

    public static string Money(decimal amount) => "₡" + amount.ToString("#,##0.00", CostaRica);

    public static string Date(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Date(DateTime? date, string whenMissing = "—") => date.HasValue ? Date(date.Value) : whenMissing;

    /// <summary>Número con coma decimal y sin ceros sobrantes: 10,67 · 5 · 0,5.</summary>
    public static string Number(decimal value, int maxDecimals = 6) =>
        decimal.Round(value, maxDecimals).ToString("#,##0." + new string('#', maxDecimals), CostaRica);

    /// <summary>
    /// El período llega de la base como «2026-10-16 — 2026-10-31»: se muestra «16/10/2026 — 31/10/2026».
    /// </summary>
    public static string PeriodText(string? period) =>
        string.IsNullOrEmpty(period)
            ? string.Empty
            : System.Text.RegularExpressions.Regex.Replace(period, @"\b(\d{4})-(\d{2})-(\d{2})\b", match => $"{match.Groups[3].Value}/{match.Groups[2].Value}/{match.Groups[1].Value}");

    /// <summary>Etiqueta de un tipo de concepto: «Deducción» con tilde.</summary>
    public static string ConceptType(string? type) => type switch
    {
        "Deduccion" => "Deducción",
        null or "" => string.Empty,
        _ => type,
    };
}
