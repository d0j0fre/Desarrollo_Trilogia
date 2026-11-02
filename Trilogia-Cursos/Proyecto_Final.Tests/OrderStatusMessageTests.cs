namespace Proyecto_Final.Tests;

/// <summary>G2-31: un cambio de estado rechazado muestra la causa que da sp_Admin_UpdateOrderStatus.</summary>
public sealed class OrderStatusMessageTests
{
    [Fact]
    public void ElCambioDeEstadoMuestraLaCausaDelProcedimiento()
    {
        var controller = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "OrdersAdminController.cs");
        var update = controller[controller.IndexOf("public async Task<IActionResult> UpdateStatus", StringComparison.Ordinal)..];

        Assert.Contains("catch (Exception ex)", update, StringComparison.Ordinal);
        Assert.Contains("BusinessErrors.MessageOr(ex, \"No fue posible actualizar el estado del pedido", update, StringComparison.Ordinal);
        Assert.Equal("La transición de estado solicitada no es válida.",
            Proyecto_Final.Services.BusinessErrors.MessageOr(SqlErrors.Create(51006, "La transición de estado solicitada no es válida."), "Genérico"));
    }
}
