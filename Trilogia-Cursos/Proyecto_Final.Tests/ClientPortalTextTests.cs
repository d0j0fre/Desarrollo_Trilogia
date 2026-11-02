using System.Text.RegularExpressions;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>Textos y estados del portal del cliente (G3-13, G3-20).</summary>
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

    [Fact]
    public void G3_20_LosEstadosDeGarantiaUsanLaPastillaLegibleDelPortal()
    {
        var vista = Vista("Warranties.cshtml");

        // Antes: badge bg-info/bg-success con el texto oscuro heredado encima del color fuerte.
        Assert.DoesNotContain("badge bg-", vista, StringComparison.Ordinal);
        Assert.Contains("<span class=\"s4-client-status-pill @claseEstado\">@solicitud.Estado</span>", vista, StringComparison.Ordinal);
        foreach (var (estado, clase) in new[] { ("Pendiente", "warning"), ("En revisión", "info"), ("Aprobada", "success"), ("Rechazada", "danger") })
        {
            Assert.Contains($"\"{estado}\" => \"{clase}\"", vista, StringComparison.Ordinal);
        }

        // La variante azul es local a la vista y cumple AA (4.5:1) para texto pequeño.
        var info = Regex.Match(vista, @"\.s4-client-status-pill\.info\s*\{\s*background:\s*(#[0-9a-f]{6});\s*color:\s*(#[0-9a-f]{6});");
        Assert.True(info.Success);
        Assert.True(Contraste(info.Groups[1].Value, info.Groups[2].Value) >= 4.5);
    }

    // Relación de contraste WCAG entre dos colores #rrggbb.
    private static double Contraste(string fondo, string texto)
    {
        static double Luminancia(string hex)
        {
            double Canal(int i)
            {
                var c = Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Canal(1) + 0.7152 * Canal(3) + 0.0722 * Canal(5);
        }

        var (a, b) = (Luminancia(fondo), Luminancia(texto));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    internal static string Vista(string archivo) =>
        MobileCorreccionesTests.Read("Proyecto_Final", "Views", "ClientPortal", archivo);
}
