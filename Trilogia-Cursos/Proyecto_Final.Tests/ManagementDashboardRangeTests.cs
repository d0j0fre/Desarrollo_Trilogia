using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

public sealed class ManagementDashboardRangeTests
{
    private static readonly DateTime Today = new(2026, 10, 20);

    [Fact]
    public void InvertedCustomRange_IsCorrectedAndWarnsTheUser()
    {
        var (desde, hasta, rango, aviso) = ManagementDashboardController.ResolveRange(
            null, new DateTime(2026, 10, 9), new DateTime(2026, 10, 1), Today);

        Assert.Equal(new DateTime(2026, 10, 1), desde);
        Assert.Equal(new DateTime(2026, 10, 9), hasta);
        Assert.Equal("personalizado", rango);
        Assert.NotNull(aviso);
        Assert.Contains("09/10/2026", aviso);
        Assert.Contains("01/10/2026", aviso);
    }

    [Fact]
    public void ValidCustomRange_HasNoWarning()
    {
        var (_, _, _, aviso) = ManagementDashboardController.ResolveRange(
            null, new DateTime(2026, 10, 1), new DateTime(2026, 10, 9), Today);

        Assert.Null(aviso);
    }

    [Theory]
    [InlineData(null, "hoy", 20, 20)]
    [InlineData("semana", "semana", 14, 20)]
    [InlineData("mes", "mes", 1, 20)]
    [InlineData("desconocido", "hoy", 20, 20)]
    public void PresetRanges_ResolveWithoutWarning(string? rango, string expected, int fromDay, int toDay)
    {
        var (desde, hasta, normalized, aviso) = ManagementDashboardController.ResolveRange(rango, null, null, Today);

        Assert.Equal(expected, normalized);
        Assert.Equal(new DateTime(2026, 10, fromDay), desde);
        Assert.Equal(new DateTime(2026, 10, toDay), hasta);
        Assert.Null(aviso);
    }

    [Fact]
    public void DetailIndicators_AreTheFourDashboardCards()
    {
        Assert.Equal(new[] { "ventas", "stock", "ruta", "cobros" }, ManagementDashboardController.Indicators);
    }
}
