using Proyecto_Final.Services;

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

    [Theory]
    [InlineData("¿Cómo registro una devolución?", "devoluciones", "Administración → Inventario → Devoluciones", "Registrar devolución")]
    [InlineData("¿Cómo creo una ruta?", "rutas", "Administración → Logística y Flota → Rutas y Entregas", "Nueva ruta")]
    [InlineData("¿Cómo genero una factura?", "facturacion", "Administración → Ventas y Pedidos → Pedidos", "Generar factura")]
    [InlineData("¿Cómo facturo un pedido?", "facturacion", "Administración → Ventas y Pedidos → Pedidos", "Generar factura")]
    public void BUG_26_02_LasPreguntasDeComoUsarUnModuloDanElPasoAPaso(string pregunta, string modulo, string navegacion, string boton)
    {
        var detectado = AssistantService.DetectarAyuda(AssistantService.Normalizar(pregunta));
        Assert.Equal(modulo, detectado);

        var respuesta = AssistantService.ResponderAyuda(detectado!);
        Assert.Equal("ayuda", respuesta.Tipo);
        Assert.Contains(navegacion, respuesta.Respuesta, StringComparison.Ordinal);
        Assert.Contains(boton, respuesta.Respuesta, StringComparison.Ordinal);
        // Paso a paso numerado, no una sola frase.
        Assert.True(respuesta.Respuesta.Split("<li>").Length - 1 >= 3);
    }

    [Fact]
    public void BUG_26_02_LaVistaDejaMostrarLaListaNumeradaDelPasoAPaso()
    {
        var view = Read("Proyecto_Final", "Views", "Assistant", "Index.cshtml");
        Assert.Contains("'UL', 'OL', 'LI'", view, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => MobileCorreccionesTests.Read(parts);
}
