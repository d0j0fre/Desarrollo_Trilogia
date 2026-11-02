namespace Proyecto_Final.Tests;

/// <summary>G2-07: la observación al liberar o descartar de cuarentena se puede escribir y se ve en el historial.</summary>
public sealed class QuarantineObservationTests
{
    [Fact]
    public void CuarentenaEnviaLaObservacionEnAmbasAcciones()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Returns", "Quarantine.cshtml");

        Assert.Contains("js-observacion-cuarentena", vista, StringComparison.Ordinal);
        Assert.Equal(2, vista.Split("<input type=\"hidden\" name=\"observacion\" data-observacion-de=\"@d.DevolucionId\" />").Length - 1);
        Assert.Contains("@section Scripts", vista, StringComparison.Ordinal);
    }

    [Fact]
    public void ElHistorialMuestraLaObservacion() =>
        Assert.Contains("@d.ObservacionResolucion", MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Returns", "Index.cshtml"), StringComparison.Ordinal);
}
