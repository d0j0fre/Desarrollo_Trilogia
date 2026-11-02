namespace Proyecto_Final.Tests;

/// <summary>G2-04: la promoción activa con la fecha de fin pasada se lista como Vencida y sin acciones.</summary>
public sealed class PromotionExpiredTests
{
    [Fact]
    public void LaListaCalculaVencidaYFiltraPorEseEstado()
    {
        var sql = MobileCorreccionesTests.Read("database", "migrations", "0257_promociones_vencidas_en_lista.sql");

        Assert.Contains("CASE WHEN pr.Estado = N'Activa' AND pr.FechaFin < @Hoy THEN N'Vencida' ELSE pr.Estado END", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE (@Estado IS NULL OR e.Estado = @Estado)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE dbo.Promociones", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SoloUnaPromocionActivaTieneBotonesDeEditarEInactivar()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Promotions", "Index.cshtml");
        var acciones = vista.IndexOf("@if (p.Estado == \"Activa\")", StringComparison.Ordinal);

        Assert.True(acciones > 0);
        Assert.True(vista.IndexOf("asp-action=\"Edit\"", acciones, StringComparison.Ordinal) > acciones);
        Assert.Contains("value=\"Vencida\"", vista, StringComparison.Ordinal);
    }
}
