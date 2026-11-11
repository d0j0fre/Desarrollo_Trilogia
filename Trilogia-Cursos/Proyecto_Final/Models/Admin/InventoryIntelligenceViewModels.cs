using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin;

public sealed class InventoryIntelligenceFilterViewModel : IValidatableObject
{
    /// <summary>Meses para el promedio de ventas: mismo valor por defecto en Inteligencia de inventario y en Sugerencias de compra.</summary>
    public const int DefaultRecentMonths = 3;

    /// <summary>Meses de cobertura: mismo valor por defecto en Inteligencia de inventario y en Sugerencias de compra.</summary>
    public const int DefaultCoverageMonths = 2;

    /// <summary>Máximo de años que puede abarcar la tendencia estacional (ambos extremos incluidos).</summary>
    public const int MaxSeasonalYears = 10;

    [Range(2000, 2100, ErrorMessage = "El año inicial debe estar entre 2000 y 2100.")]
    public int StartYear { get; set; } = DateTime.UtcNow.Year - 2;

    [Range(2000, 2100, ErrorMessage = "El año final debe estar entre 2000 y 2100.")]
    public int EndYear { get; set; } = DateTime.UtcNow.Year;

    [Range(1, 12, ErrorMessage = "Los meses para el promedio deben estar entre 1 y 12.")]
    public int RecentMonths { get; set; } = DefaultRecentMonths;

    [Range(1, 12, ErrorMessage = "Los meses de cobertura deben estar entre 1 y 12.")]
    public int CoverageMonths { get; set; } = DefaultCoverageMonths;

    [Range(30, 365, ErrorMessage = "La ventana sin rotación debe estar entre 30 y 365 días.")]
    public int SlowWindowDays { get; set; } = 60;

    /// <summary>Categoría de producto de la tendencia estacional; vacía = todas.</summary>
    [StringLength(100, ErrorMessage = "La categoría no es válida.")]
    public string? Category { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // BUG-24-06: un solo mensaje por problema (si se asociara a los dos campos saldría repetido).
        if (StartYear > EndYear)
        {
            yield return new ValidationResult(
                "El año inicial no puede ser mayor que el año final.",
                new[] { nameof(StartYear) });
        }
        else if (EndYear - StartYear + 1 > MaxSeasonalYears)
        {
            yield return new ValidationResult(
                $"El período estacional no puede superar {MaxSeasonalYears} años.",
                new[] { nameof(StartYear) });
        }
    }
}

public sealed class InventoryIntelligenceViewModel
{
    public InventoryIntelligenceFilterViewModel Filter { get; set; } = new();
    public List<PurchaseSuggestionItem> SugerenciasCompra { get; set; } = new();
    public List<SlowMovingProductItem> ProductosEstancados { get; set; } = new();
    public List<SeasonalTrendPoint> TendenciaEstacional { get; set; } = new();
    public List<string> Categorias { get; set; } = new();
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PurchaseSuggestionItem
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public int UnidadesVendidasVentana { get; set; }
    public decimal PromedioVentaMensual { get; set; }
    public int MesesCobertura { get; set; }
    public int CantidadSugerida { get; set; }
    public bool DatosInsuficientes { get; set; }
}

public sealed class SlowMovingProductItem
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Stock { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? UltimaVenta { get; set; }
    public int DiasSinMovimiento { get; set; }
    public int VendidoEnVentana { get; set; }
    public string NivelRiesgo { get; set; } = string.Empty;
}

public sealed class SeasonalTrendPoint
{
    public int NumeroMes { get; set; }
    public string NombreMes { get; set; } = string.Empty;
    public decimal TotalVendido { get; set; }
    public int UnidadesVendidas { get; set; }
}

public static class InventoryIntelligencePolicy
{
    private static readonly string[] MonthNames =
    {
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    };

    public static int CalculateSuggestedQuantity(
        int unitsSold,
        int recentMonths,
        int coverageMonths,
        int safetyStock,
        int currentStock)
    {
        if (recentMonths <= 0 || coverageMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentMonths));
        }

        var average = Math.Max(unitsSold, 0) / (decimal)recentMonths;
        var target = checked((int)Math.Ceiling(average * coverageMonths) + Math.Max(safetyStock, 0));
        return Math.Max(checked(target - Math.Max(currentStock, 0)), 0);
    }

    public static List<SeasonalTrendPoint> NormalizeTwelveMonths(IEnumerable<SeasonalTrendPoint> source)
    {
        var byMonth = source
            .Where(point => point.NumeroMes is >= 1 and <= 12)
            .GroupBy(point => point.NumeroMes)
            .ToDictionary(
                group => group.Key,
                group => new SeasonalTrendPoint
                {
                    NumeroMes = group.Key,
                    NombreMes = MonthNames[group.Key - 1],
                    TotalVendido = group.Sum(point => point.TotalVendido),
                    UnidadesVendidas = group.Sum(point => point.UnidadesVendidas)
                });

        return Enumerable.Range(1, 12)
            .Select(month => byMonth.GetValueOrDefault(month) ?? new SeasonalTrendPoint
            {
                NumeroMes = month,
                NombreMes = MonthNames[month - 1]
            })
            .ToList();
    }
}
