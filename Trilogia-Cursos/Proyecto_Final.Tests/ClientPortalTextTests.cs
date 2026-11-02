using System.Text.RegularExpressions;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>Textos del portal del cliente (G3-13).</summary>
public sealed class ClientPortalTextTests
{
    public static TheoryData<string> Vistas => new()
    {
        "Index.cshtml", "Detail.cshtml", "Statement.cshtml", "Invoice.cshtml", "Invoices.cshtml",
        "Warranty.cshtml", "Warranties.cshtml",
    };

    [Theory]
    [MemberData(nameof(Vistas))]
    public void G3_13_LosTextosVisiblesLlevanTilde(string vista)
    {
        var source = Vista(vista);

        // En minúscula solo aparecen como texto (los identificadores van en mayúscula: Model.Credito).
        Assert.DoesNotMatch(new Regex(@"\b(credito|telefono|direccion|informacion|cancelacion)\b"), source);
        // Al inicio de una etiqueta también son texto visible.
        Assert.DoesNotMatch(new Regex(@">\s*(Credito|Telefono|Direccion|Informacion|Ultimo|Limite|Numero)\b"), source);
        Assert.DoesNotContain("\"Si, cancelar\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void G3_13_ElDetalleConfirmaLaCancelacionConTildes()
    {
        var detalle = Vista("Detail.cshtml");

        Assert.Contains("data-s3-confirm-title=\"Confirmar cancelación\"", detalle, StringComparison.Ordinal);
        Assert.Contains("data-s3-confirm-ok=\"Sí, cancelar\"", detalle, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Index.cshtml", "pedido.Estado", 1)]
    [InlineData("Detail.cshtml", "Model.Estado", 2)]
    [InlineData("Statement.cshtml", "pedido.Estado", 1)]
    public void G3_13_ElEstadoDelPedidoSeMuestraConSuNombre(string vista, string estado, int veces)
    {
        var source = Vista(vista);

        Assert.Equal(veces, source.Split($"@OrderStatusFlow.Name({estado})").Length - 1);
        Assert.DoesNotContain($"@{estado}<", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("EnProceso", "En proceso")]
    [InlineData("EnRuta", "En ruta")]
    [InlineData("Fallido", "No entregado")]
    [InlineData("Pendiente", "Pendiente")]
    public void G3_13_LosCodigosDeEstadoTienenNombreLegible(string codigo, string nombre) =>
        Assert.Equal(nombre, OrderStatusFlow.Name(codigo));

    internal static string Vista(string archivo) =>
        MobileCorreccionesTests.Read("Proyecto_Final", "Views", "ClientPortal", archivo);
}
