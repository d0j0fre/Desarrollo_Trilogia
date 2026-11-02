namespace Proyecto_Final.Tests;

/// <summary>G2-09 / G3-12: botones «Volver» e insignias legibles sobre encabezados oscuros.</summary>
public sealed class DarkHeaderContrastTests
{
    private static string Css() => MobileCorreccionesTests.Read("Proyecto_Final", "wwwroot", "css", "design-system-buttons.css");

    private static string ReglaQueContiene(string css, string selector)
    {
        var i = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(i >= 0, $"No está el selector {selector}.");
        return css[i..css.IndexOf('}', i)];
    }

    [Fact]
    public void ElBotonNeutroDeUnEncabezadoAdministrativoUsaTintaClara() =>
        Assert.Contains("color: var(--ds-ink-light) !important;", ReglaQueContiene(Css(), ".admin-page-header .btn-ds-neutral,"), StringComparison.Ordinal);

    [Fact]
    public void LaInsigniaDePendientesEsSolida()
    {
        var regla = ReglaQueContiene(Css(), ".admin-page-header .badge-warning {");

        Assert.Contains("background-color: var(--brand-gold-primary, #D4AF37) !important;", regla, StringComparison.Ordinal);
        Assert.Contains("color: var(--brand-black, #1F1F1F) !important;", regla, StringComparison.Ordinal);
    }
}
