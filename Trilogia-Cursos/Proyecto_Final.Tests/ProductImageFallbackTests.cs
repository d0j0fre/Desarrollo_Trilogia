using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>G3-11: un producto sin imagen (o con una que no existe) no muestra la foto de un whisky.</summary>
public sealed class ProductImageFallbackTests
{
    private static string WwwRoot(string appRelative) =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Proyecto_Final", "wwwroot", appRelative.TrimStart('~', '/'));

    [Theory]
    [InlineData("Cerveza", "~/img/CAT-Cerveza.png")]
    [InlineData("Vino", "~/img/CAT-Vino.png")]
    [InlineData("Ron", "~/img/CAT- Ron.png")]
    [InlineData("Ginebra", "~/img/CAT-Gin.png")]
    [InlineData("Supermercado", ProductImageFallback.Neutral)]
    [InlineData(null, ProductImageFallback.Neutral)]
    public void CadaCategoriaTieneUnRespaldoQueExiste(string? categoria, string esperado)
    {
        var respaldo = ProductImageFallback.ForCategory(categoria);

        Assert.Equal(esperado, respaldo);
        Assert.True(File.Exists(WwwRoot(respaldo)), $"No existe {respaldo}.");
    }

    [Fact]
    public void LaImagenGuardadaSeRespetaYLaFaltanteUsaElRespaldo()
    {
        Assert.Equal("~/img/productos/pilsen-6pack.png", ProductImageFallback.Resolve("~/img/productos/pilsen-6pack.png", "Cerveza"));
        Assert.Equal("~/img/CAT-Cerveza.png", ProductImageFallback.Resolve(null, "Cerveza"));
        // Los procedimientos de la Tienda devuelven la foto del whisky cuando no hay imagen.
        Assert.Equal(ProductImageFallback.Neutral, ProductImageFallback.Resolve("~/img/whisky-premium.webp", "Supermercado"));
        Assert.Equal("~/img/CAT-Cerveza.png", ProductImageFallback.Resolve("~/img/whisky-premium.webp", "Cerveza"));
    }

    [Theory]
    [InlineData("Cart", "Index.cshtml")]
    [InlineData("Home", "Shop.cshtml")]
    [InlineData("Home", "Detail.cshtml")]
    [InlineData("Inventory", "Index.cshtml")]
    public void LasPantallasNoUsanLaFotoDeWhiskyComoRespaldo(string carpeta, string vista)
    {
        var source = MobileCorreccionesTests.Read("Proyecto_Final", "Views", carpeta, vista);

        Assert.DoesNotContain("whisky-premium.webp", source, StringComparison.Ordinal);
        Assert.Contains("ProductImageFallback", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ElCarritoNoApuntaAUnaImagenQueNoExiste() =>
        Assert.Equal(string.Empty, new Proyecto_Final.Models.Store.CartItemViewModel().ImagenUrl);
}
