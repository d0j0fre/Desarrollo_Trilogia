namespace Proyecto_Final.Tests;

/// <summary>
/// Correcciones del bloque Asistente virtual y sugerencias en Venta móvil (QA G8).
/// </summary>
public sealed class AssistantCorreccionesTests
{
    [Fact]
    public void BUG_26_01_LaPreguntaVaciaMuestraEscribeUnaPregunta()
    {
        var view = Read("Proyecto_Final", "Views", "Assistant", "Index.cshtml");
        var controller = Read("Proyecto_Final", "Controllers", "AssistantController.cs");

        // El cliente no envía la pregunta vacía, pero la avisa en la conversación
        // con el mismo texto que devuelve el servidor.
        Assert.Contains("escapeHtml('Escribe una pregunta.')", view, StringComparison.Ordinal);
        Assert.Contains("\"Escribe una pregunta.\"", controller, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => MobileCorreccionesTests.Read(parts);
}
