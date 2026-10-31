using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class WhatsAppReceiptLinkTests
{
    [Theory]
    [InlineData("88880002", "50688880002")]
    [InlineData("8888-0002", "50688880002")]
    [InlineData(" 8888 0002 ", "50688880002")]
    [InlineData("506 8888-0002", "50688880002")]
    [InlineData("+506 8888 0002", "50688880002")]
    public void NormalizePhone_RemovesSeparatorsAndAddsCostaRicaPrefix(string telefono, string expected)
    {
        Assert.Equal(expected, WhatsAppReceiptLink.NormalizePhone(telefono));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0000-0000")]
    [InlineData("88-80")]
    [InlineData("1 202 555 0100")]
    public void NormalizePhone_WithoutUsableNumber_ReturnsNull(string? telefono)
    {
        Assert.Null(WhatsAppReceiptLink.NormalizePhone(telefono));
        Assert.Null(WhatsAppReceiptLink.BuildUrl(telefono, "FAC-1", new DateTime(2026, 10, 30), 1850m));
    }

    [Fact]
    public void BuildUrl_EncodesMessageWithInvoiceDateAndTotalOnly()
    {
        var url = WhatsAppReceiptLink.BuildUrl("8888-0002", "FAC-20261030-0001", new DateTime(2026, 10, 30, 18, 23, 0), 1850m);

        Assert.NotNull(url);
        Assert.StartsWith("https://wa.me/50688880002?text=", url);
        var texto = url!["https://wa.me/50688880002?text=".Length..];
        Assert.DoesNotContain(" ", texto);
        Assert.DoesNotContain("&", texto);

        var mensaje = Uri.UnescapeDataString(texto);
        Assert.Equal(WhatsAppReceiptLink.BuildMessage("FAC-20261030-0001", new DateTime(2026, 10, 30), 1850m), mensaje);
        Assert.Contains("FAC-20261030-0001", mensaje);
        Assert.Contains("30/10/2026", mensaje);
        Assert.Contains("₡1", mensaje);
        Assert.DoesNotContain("@", mensaje);
    }
}
