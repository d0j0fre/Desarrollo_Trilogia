namespace Proyecto_Final.Tests;

/// <summary>G2-06: rechazar un pedido retenido valida el motivo antes de pedir confirmación.</summary>
public sealed class RejectRetainedOrderTests
{
    [Fact]
    public void ElRechazoTieneVentanaPropiaYValidaAntes()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "ManagerOrders", "Review.cshtml");
        var form = vista[vista.IndexOf("id=\"rechazarPedidoForm\"", StringComparison.Ordinal)..];

        Assert.Contains("data-s3-confirm-title=\"Rechazar pedido #@Model.Pedido.PedidoId\"", form[..600], StringComparison.Ordinal);
        Assert.Contains("addEventListener('click'", vista, StringComparison.Ordinal);
        Assert.Contains("jQuery(form).valid()", vista, StringComparison.Ordinal);
    }

    [Fact]
    public void ElScriptGenericoNoPideUnaSegundaConfirmacion() =>
        // sprint4-admin.js deja pasar los formularios que declaran su propia confirmación.
        Assert.Contains("if (form.hasAttribute(\"data-s3-confirm\")) return;",
            MobileCorreccionesTests.Read("Proyecto_Final", "wwwroot", "js", "sprint4-admin.js"), StringComparison.Ordinal);
}
