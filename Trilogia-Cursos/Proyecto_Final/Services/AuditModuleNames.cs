using System.Globalization;
using System.Text;

namespace Proyecto_Final.Services;

/// <summary>
/// M-14: la bitácora guarda el módulo tal como lo escribió cada pantalla (Facturacion, Facturación, RRHH, RR. HH.…).
/// La lista del filtro de Auditoría muestra un solo nombre por módulo, con tildes, y al filtrar se consultan todas las
/// variantes guardadas. La tabla de auditoría no se reescribe.
/// </summary>
public static class AuditModuleNames
{
    // Nombre correcto de los módulos que algunas pantallas guardan sin tilde o con otra grafía.
    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        ["facturacion"] = "Facturación",
        ["garantias"] = "Garantías",
        ["movil"] = "Móvil",
        ["rrhh"] = "RR. HH.",
        ["auditoria"] = "Auditoría",
        ["creditos"] = "Créditos",
        ["categorias"] = "Categorías",
        ["devoluciones"] = "Devoluciones",
        ["configuracion"] = "Configuración",
        ["gestion"] = "Gestión",
        ["logistica"] = "Logística",
        ["produccion"] = "Producción",
        ["promociones"] = "Promociones",
        ["inventario"] = "Inventario",
        ["transformacion"] = "Transformación",
        ["comunicacion"] = "Comunicación",
        ["notificaciones"] = "Notificaciones",
        ["metasykpis"] = "Metas y KPIs",
    };

    /// <summary>Clave de comparación: sin tildes, sin signos ni espacios y en minúscula («RR. HH.» y «RRHH» dan «rrhh»).</summary>
    public static string Key(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Nombre a mostrar de un módulo guardado: el conocido con tildes o, si no se conoce, el mismo texto.</summary>
    public static string DisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return Known.TryGetValue(Key(name), out var known) ? known : name.Trim();
    }

    /// <summary>Una opción por módulo para la lista del filtro, ordenada y sin variantes repetidas.</summary>
    public static List<string> Options(IEnumerable<string> storedNames) =>
        storedNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .GroupBy(Key)
            .Select(g => Known.TryGetValue(g.Key, out var known)
                ? known
                : g.OrderByDescending(HasAccent).ThenBy(n => n, StringComparer.Ordinal).First().Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Create(CultureInfo.GetCultureInfo("es-CR"), ignoreCase: true))
            .ToList();

    /// <summary>Todas las grafías guardadas del módulo elegido (la elegida siempre se incluye).</summary>
    public static List<string> StoredVariants(string selected, IEnumerable<string> storedNames)
    {
        var key = Key(selected);
        var variants = storedNames.Where(n => !string.IsNullOrWhiteSpace(n) && Key(n) == key)
            .Select(n => n.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (!variants.Contains(selected.Trim(), StringComparer.Ordinal)) variants.Add(selected.Trim());
        return variants;
    }

    private static bool HasAccent(string name) => name.Any(c => c > 127 && char.IsLetter(c));
}
