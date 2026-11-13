using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>G3-19: «Crédito vendedor» (histórico) se muestra como «Pago contra entrega».</summary>
public sealed class PaymentMethodNamesTests
{
    [Theory]
    [InlineData("Crédito vendedor", "Pago contra entrega")]
    [InlineData("Efectivo contra entrega", "Efectivo contra entrega")]
    [InlineData("Crédito", "Crédito")]
    [InlineData("", "No registrado")]
    [InlineData(null, "No registrado")]
    public void ElMetodoSeMuestraSinConfundirseConCredito(string? guardado, string mostrado) =>
        Assert.Equal(mostrado, PaymentMethodNames.Display(guardado));

    [Theory]
    [InlineData("OrdersAdmin", "Detail.cshtml", "PaymentMethodNames.Display(Model.MetodoPago)")]
    [InlineData("Finance", "Liquidate.cshtml", "PaymentMethodNames.Display(p.MetodoPago)")]
    public void LasPantallasUsanElNombreLegible(string carpeta, string vista, string uso) =>
        Assert.Contains(uso, MobileCorreccionesTests.Read("Proyecto_Final", "Views", carpeta, vista), StringComparison.Ordinal);
}
