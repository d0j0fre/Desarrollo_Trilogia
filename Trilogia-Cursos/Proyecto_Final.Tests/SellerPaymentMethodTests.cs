using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

/// <summary>G2-39: un pedido de Venta móvil contra entrega se guarda como «Efectivo contra entrega».</summary>
public sealed class SellerPaymentMethodTests
{
    [Fact]
    public void ElProcedimientoGuardaEfectivoContraEntregaPorDefecto()
    {
        var sql = MobileCorreccionesTests.Read("database", "migrations", "0255_venta_movil_pago_contra_entrega.sql");

        Assert.Contains("ISNULL(@MetodoPago, N'Efectivo contra entrega')", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ISNULL(@MetodoPago, N'Crédito vendedor')", sql, StringComparison.Ordinal);
        // El cargo al crédito de la 0230 sigue igual.
        Assert.Contains("IF @MetodoPago = N'Crédito'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void LaAplicacionSoloEnviaMetodoParaElCredito()
    {
        // Contra entrega no envía @MetodoPago: el procedimiento pone «Efectivo contra entrega»,
        // y Liquidación de cobros cuenta como efectivo los métodos que contienen «Efectivo».
        var service = MobileCorreccionesTests.Read("Proyecto_Final", "Services", "AdminDbService.cs");

        Assert.Contains("if (model.MetodoPago == SellerOrderCreateViewModel.PagoCredito)", service, StringComparison.Ordinal);
        Assert.Equal("Contra entrega", SellerOrderCreateViewModel.PagoContraEntrega);
    }
}
