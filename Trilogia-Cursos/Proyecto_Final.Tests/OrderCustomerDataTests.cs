using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

/// <summary>G3-15: los datos del cliente del pago se guardan con el pedido y se ven en su detalle.</summary>
public sealed class OrderCustomerDataTests
{
    [Fact]
    public void ElCheckoutGuardaLosDatosDespuesDeCrearElPedido()
    {
        var cart = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "CartController.cs");
        var crear = cart.IndexOf("CreateOrderWithPromotionsAsync(", StringComparison.Ordinal);
        var guardar = cart.IndexOf("SaveOrderCustomerDataAsync(order.PedidoId, usuarioId, model", StringComparison.Ordinal);

        Assert.True(crear > 0 && guardar > crear);
    }

    [Fact]
    public void ElServicioEnviaTodosLosDatosDelPago()
    {
        var service = MobileCorreccionesTests.Read("Proyecto_Final", "Services", "StoreDbService.cs");

        foreach (var parametro in new[] { "@TipoCliente", "@FacturaElectronica", "@CorreoComprobante", "@Telefono", "@TelefonoAlterno" })
        {
            Assert.Contains($"\"{parametro}\"", service, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ElProcedimientoSoloAceptaPedidosDelMismoCliente()
    {
        var sql = MobileCorreccionesTests.Read("database", "migrations", "0256_pedido_datos_cliente.sql");

        Assert.Contains("WHERE PedidoId = @PedidoId AND UsuarioId = @UsuarioId", sql, StringComparison.Ordinal);
        Assert.Contains("MERGE dbo.PedidoDatosCliente", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ElDetalleDelPedidoMuestraLosDatos()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "OrdersAdmin", "Detail.cshtml");

        Assert.Contains("ViewData[\"DatosCliente\"] is Proyecto_Final.Models.Store.OrderCustomerDataViewModel", vista, StringComparison.Ordinal);
        Assert.Contains(typeof(OrdersAdminController).GetConstructors().Single().GetParameters(), p => p.ParameterType.Name == "StoreDbService");
    }
}
