namespace Proyecto_Final.Tests;

/// <summary>G3-12: «Volver a Mis pedidos» legible y la etiqueta de estado separada de «Cancelar pedido».</summary>
public sealed class ClientDetailHeaderTests
{
    [Fact]
    public void ElBotonDelEncabezadoDelPortalUsaTintaClara()
    {
        var css = MobileCorreccionesTests.Read("Proyecto_Final", "wwwroot", "css", "design-system-buttons.css");
        var i = css.IndexOf(".s4-client-portal-hero .btn-ds-neutral,", StringComparison.Ordinal);

        Assert.True(i > 0);
        Assert.Contains("color: var(--ds-ink-light) !important;", css[i..css.IndexOf('}', i)], StringComparison.Ordinal);
    }

    [Fact]
    public void LaEtiquetaDeEstadoQuedaSeparada() =>
        Assert.Contains("s4-client-status-pill @claseEstado d-inline-block mt-3",
            MobileCorreccionesTests.Read("Proyecto_Final", "Views", "ClientPortal", "Detail.cshtml"), StringComparison.Ordinal);
}
