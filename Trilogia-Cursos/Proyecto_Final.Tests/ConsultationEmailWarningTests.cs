namespace Proyecto_Final.Tests;

/// <summary>G2-43: si falla el correo al resolver una consulta, sale un solo aviso amarillo.</summary>
public sealed class ConsultationEmailWarningTests
{
    [Fact]
    public void ElFalloDelCorreoReemplazaElAvisoVerdePorUnoAmarillo()
    {
        var controller = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "ConsultationsController.cs");
        var falla = controller[controller.IndexOf("No se pudo notificar la resolución de la consulta", StringComparison.Ordinal)..];
        falla = falla[..falla.IndexOf('}')];

        Assert.Contains("TempData.Remove(\"SuccessMessage\");", falla, StringComparison.Ordinal);
        Assert.Contains("TempData[\"WarningMessage\"]", falla, StringComparison.Ordinal);
        Assert.DoesNotContain("TempData[\"ErrorMessage\"]", falla, StringComparison.Ordinal);
    }

    [Fact]
    public void ElLayoutMuestraElAvisoAmarilloUnaSolaVez()
    {
        var layout = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Shared", "_Layout.cshtml");
        var linea = layout.Split('\n').Single(l => l.Contains("TempData[\"WarningMessage\"]", StringComparison.Ordinal));

        Assert.Contains("!ShownByView(warningMessage)", linea, StringComparison.Ordinal);
        Assert.Contains("alert alert-warning\">@warningMessage", layout, StringComparison.Ordinal);
    }
}
