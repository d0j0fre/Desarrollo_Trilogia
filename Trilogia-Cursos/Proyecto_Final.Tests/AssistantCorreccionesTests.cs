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

    [Theory]
    [InlineData("¿Cuánto se vendió este mes?")]
    [InlineData("¿Cuál es el saldo de créditos?")]
    [InlineData("¿Cómo van las ventas?")]
    [InlineData("Dame un resumen de ventas")]
    [InlineData("¿Cuánto se vendió hoy?")]
    [InlineData("Más detalle de las ventas")]
    public void BUG_26_06_ElChoferRecibeInformacionRestringidaEnLasPreguntasDeVentas(string pregunta)
    {
        var respuesta = AssistantService.Restringida(AssistantService.Normalizar(pregunta), "Chofer");

        Assert.NotNull(respuesta);
        Assert.Equal("Información restringida", respuesta!.Titulo);
        Assert.Contains("Finanzas", respuesta.Respuesta, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Administrador", "¿Cuánto se vendió este mes?")]
    [InlineData("Gerente", "¿Cómo van las ventas?")]
    [InlineData("Chofer", "Dame un resumen")]
    [InlineData("Chofer", "¿Cuántas entregas pendientes hay?")]
    [InlineData("Chofer", "¿Cómo genero una factura?")]
    [InlineData("Chofer", "¿Qué puedo preguntar?")]
    [InlineData("Vendedor", "¿Cómo está el inventario?")]
    [InlineData("Bodeguero", "¿Cómo está el inventario?")]
    public void BUG_26_06_LoPermitidoNoSeRestringe(string rol, string pregunta)
    {
        Assert.Null(AssistantService.Restringida(AssistantService.Normalizar(pregunta), rol));
    }

    [Fact]
    public void BUG_26_06_ElInventarioNoSeConfundeConVentas()
    {
        // "inVENTArio" contenía la palabra clave "venta": al Chofer se le decía que
        // Finanzas estaba restringida y a quien sí podía ver, se le daban las ventas.
        var respuesta = AssistantService.Restringida(AssistantService.Normalizar("¿Cómo está el inventario?"), "Chofer");

        Assert.NotNull(respuesta);
        Assert.Contains("Inventario", respuesta!.Respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("Finanzas", respuesta.Respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public void BUG_26_04_LasEntregasPendientesSonLasParadasAbiertasDeLasRutasDespachadas()
    {
        var migration = Read("database", "migrations", "0250_asistente_entregas_pendientes.sql");
        var readme = Read("database", "migrations", "README.md");

        // Misma regla que la columna "Pendientes" de sp_Rutas_List, solo en rutas despachadas.
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Asistente_Metricas", migration, StringComparison.Ordinal);
        Assert.Contains("WHERE r.Estado = N'Despachada'", migration, StringComparison.Ordinal);
        Assert.Contains("AND rp.EstadoEntrega IN (N'Pendiente', N'EnRuta');", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("@EntregasPendientes = SUM(CASE WHEN EstadoEntrega = N'Pendiente'", migration, StringComparison.Ordinal);
        // Solo lee: no toca tablas ni procedimientos de rutas.
        Assert.DoesNotContain("PROCEDURE dbo.sp_Rutas", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", migration, StringComparison.Ordinal);
        Assert.Contains("0250_asistente_entregas_pendientes.sql", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void BUG_26_03_LaConversacionSePuedeExportarAExcel()
    {
        var view = Read("Proyecto_Final", "Views", "Assistant", "Index.cshtml");
        var script = Read("Proyecto_Final", "wwwroot", "js", "xlsx-export.js");

        Assert.Contains("id=\"assistantExport\"", view, StringComparison.Ordinal);
        Assert.Contains("~/js/xlsx-export.js", view, StringComparison.Ordinal);
        Assert.Contains("window.XlsxExport.download(", view, StringComparison.Ordinal);
        Assert.Contains("'.xlsx'", view, StringComparison.Ordinal);

        // Libro OpenXML real (no CSV renombrado) y celdas de texto que Excel no evalúa como fórmulas.
        Assert.Contains("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", script, StringComparison.Ordinal);
        Assert.Contains("name: 'xl/worksheets/sheet1.xml'", script, StringComparison.Ordinal);
        Assert.Contains("t=\"inlineStr\"", script, StringComparison.Ordinal);
        Assert.Contains(".replace(/</g, '&lt;')", script, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => MobileCorreccionesTests.Read(parts);
}
