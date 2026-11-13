namespace Proyecto_Final.Tests;

/// <summary>G2-08: la pastilla «info» (Prestado, comodato Activo, Mayorista, En proceso) tiene color.</summary>
public sealed class BadgeInfoStyleTests
{
    [Fact]
    public void LaPastillaInfoTieneFondoYTextoConContraste()
    {
        var css = MobileCorreccionesTests.Read("Proyecto_Final", "wwwroot", "css", "custom-theme.css");
        var regla = css[css.IndexOf(".admin-badge-soft.info {", StringComparison.Ordinal)..];
        regla = regla[..regla.IndexOf('}')];

        Assert.Contains("background: rgba(30, 79, 153, .12);", regla, StringComparison.Ordinal);
        Assert.Contains("color: #1e4f99;", regla, StringComparison.Ordinal);
        // #1e4f99 sobre el fondo azul al 12 % sobre blanco (≈ #e4eaf3): contraste > 4.5:1.
        Assert.True(Contrast((0x1e, 0x4f, 0x99), (0xe4, 0xea, 0xf3)) >= 4.5);
    }

    private static double Contrast((int R, int G, int B) a, (int R, int G, int B) b)
    {
        static double L((int R, int G, int B) c)
        {
            static double Ch(int v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }
        var (hi, lo) = (Math.Max(L(a), L(b)), Math.Min(L(a), L(b)));
        return (hi + 0.05) / (lo + 0.05);
    }
}
