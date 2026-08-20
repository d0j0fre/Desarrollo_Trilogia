using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class OrderReceiptHtmlBuilderTests
{
    private const string Template = "TOTAL={{TOTAL}}|SUBTOTAL={{SUBTOTAL}}|{{DESCUENTO}}|{{PRODUCTOS}}|CLIENTE={{CLIENTE}}";

    [Fact]
    public void Receipt_UsesAuthoritativeTotalAfterDiscount()
    {
        var html = Build(
            [new CartItemViewModel { Nombre = "Producto", Precio = 10000m, Cantidad = 1, MontoDescuento = 1000m }],
            9000m,
            1000m);

        Assert.Contains($"TOTAL={OrderReceiptHtmlBuilder.FormatCurrency(9000m)}", html);
        Assert.Contains("Descuento:", html);
    }

    [Fact]
    public void Receipt_ShowsComboQuantityAndAuthoritativeTotal()
    {
        var html = Build(
            [new CartItemViewModel
            {
                ItemType = CartItemTypes.Combo,
                Nombre = "Café y galletas",
                Precio = 8000m,
                Cantidad = 2
            }],
            16000m,
            0m);

        Assert.Contains("Combo:", html);
        Assert.Contains("galletas", html);
        Assert.Contains(OrderReceiptHtmlBuilder.FormatCurrency(16000m), html);
    }

    [Fact]
    public void Receipt_ShowsGiftsAtZeroWithoutIncreasingTheTotal()
    {
        var html = Build(
            [new CartItemViewModel
            {
                Nombre = "Regalo promocional",
                Precio = 1500m,
                Cantidad = 1,
                EsRegalo = true,
                PromocionNombre = "Cliente frecuente"
            }],
            0m,
            0m);

        Assert.Contains("Regalo", html);
        Assert.Contains(OrderReceiptHtmlBuilder.FormatCurrency(0m), html);
        Assert.Contains($"TOTAL={OrderReceiptHtmlBuilder.FormatCurrency(0m)}", html);
    }

    [Fact]
    public void Receipt_MixedOrderKeepsSqlConfirmedTotalAndEscapesText()
    {
        var html = Build(
        [
            new CartItemViewModel { Nombre = "Producto <script>", Precio = 10000m, Cantidad = 1, MontoDescuento = 1000m },
            new CartItemViewModel { ItemType = CartItemTypes.Combo, Nombre = "Combo", Precio = 8000m, Cantidad = 2 },
            new CartItemViewModel { Nombre = "Regalo", Precio = 100m, Cantidad = 1, EsRegalo = true }
        ],
        25000m,
        1000m);

        Assert.Contains($"TOTAL={OrderReceiptHtmlBuilder.FormatCurrency(25000m)}", html);
        Assert.Contains("Producto &lt;script&gt;", html);
        Assert.DoesNotContain("Producto <script>", html);
    }

    private static string Build(
        IReadOnlyCollection<CartItemViewModel> items,
        decimal totalConfirmado,
        decimal descuentoTotal) => OrderReceiptHtmlBuilder.Build(
            Template,
            "Cliente",
            42,
            new CheckoutViewModel
            {
                MetodoPago = "Tarjeta demo",
                TipoEntrega = "Retiro en tienda",
                DireccionEntrega = "Sucursal"
            },
            items,
            totalConfirmado,
            descuentoTotal,
            new DateTime(2026, 7, 27, 10, 0, 0),
            new CompanyOptions { BrandName = "Distribuidora JJ", BrandSubtitle = "Licorera - Distribuidora" });
}
