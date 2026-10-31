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

    [Theory]
    [InlineData("inventario")] [InlineData("pedidos")] [InlineData("facturacion")] [InlineData("clientes")]
    [InlineData("creditos")] [InlineData("cobros")] [InlineData("rutas")] [InlineData("vehiculos")]
    [InlineData("kilometraje")] [InlineData("mantenimiento")] [InlineData("alertas")] [InlineData("activos")]
    [InlineData("devoluciones")] [InlineData("cuarentena")] [InlineData("empleados")] [InlineData("roles")]
    [InlineData("consultas")]
    public void BUG_26_02_LaNavegacionDeLaAyudaExisteEnElMenu(string modulo)
    {
        var layout = Read("Proyecto_Final", "Views", "Shared", "_Layout.cshtml");
        var texto = System.Text.RegularExpressions.Regex.Replace(AssistantService.ResponderAyuda(modulo).Respuesta, "<[^>]+>", string.Empty);

        Assert.DoesNotContain("Operación →", texto, StringComparison.Ordinal);
        var rutas = System.Text.RegularExpressions.Regex.Matches(texto, @"Administración → ([^→]+?) → ");
        Assert.NotEmpty(rutas);
        foreach (System.Text.RegularExpressions.Match ruta in rutas)
        {
            // El área es una sección del menú y lo que sigue empieza con una opción de esa sección.
            var area = ruta.Groups[1].Value.Trim();
            var inicioSeccion = layout.IndexOf($"acc-sec__label\">{area}</span>", StringComparison.Ordinal);
            Assert.True(inicioSeccion >= 0, $"No existe la sección «{area}» en el menú.");
            var finSeccion = layout.IndexOf("</details>", inicioSeccion, StringComparison.Ordinal);
            var opciones = System.Text.RegularExpressions.Regex.Matches(layout[inicioSeccion..finSeccion], "<span>([^<]+)</span>")
                .Select(m => m.Groups[1].Value);
            var resto = texto[(ruta.Index + ruta.Length)..];
            Assert.True(opciones.Any(o => resto.StartsWith(o, StringComparison.Ordinal)),
                $"«{resto[..Math.Min(40, resto.Length)]}» no es una opción de «{area}» en el menú.");
        }
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

    [Fact]
    public void BUG_26_05_SeSugiereUnArticuloDeOtraCategoriaQueElVendedorPuedeAgregar()
    {
        var sugerencias = new[]
        {
            Sugerencia(12, "Imperial Silver 6 Pack", "Cerveza"),
            Sugerencia(99, "Producto fuera del catálogo", "Snacks"),
            Sugerencia(30, "Agotado", "Hielo", stock: 0),
            Sugerencia(23, "Bolsa de Hielo 2kg", "Hielo"),
            Sugerencia(24, "Yucas Tostadas 150g", "Snacks"),
        };
        var catalogo = new HashSet<int> { 12, 13, 23, 24, 30 };
        var categorias = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cerveza" };

        var elegida = Proyecto_Final.Models.Admin.SellerCrossSellPolicy.Complementario(sugerencias, categorias, catalogo);

        // Con cerveza no se sugiere otra cerveza, ni algo que no está en el catálogo o sin stock.
        Assert.NotNull(elegida);
        Assert.Equal(23, elegida!.ProductoId);
    }

    [Fact]
    public void BUG_26_05_SinComplementarioNoHaySugerencia()
    {
        var sugerencias = new[] { Sugerencia(12, "Imperial Silver 6 Pack", "Cerveza") };
        var categorias = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cerveza" };

        Assert.Null(Proyecto_Final.Models.Admin.SellerCrossSellPolicy.Complementario(sugerencias, categorias, new HashSet<int> { 12 }));
    }

    [Theory]
    [InlineData("13", new[] { 13 })]
    [InlineData("13, 2,13,x,-4,0", new[] { 13, 2 })]
    [InlineData("", new int[0])]
    [InlineData(null, new int[0])]
    public void BUG_26_05_LaListaDeProductosSeValida(string? productos, int[] esperados)
    {
        Assert.Equal(esperados.ToHashSet(), Proyecto_Final.Models.Admin.SellerCrossSellPolicy.ParseProductos(productos));
    }

    [Fact]
    public void BUG_26_05_VentaMovilMuestraLaSugerenciaYNoInsisteAlDescartarla()
    {
        var view = Read("Proyecto_Final", "Views", "SellerOrders", "Index.cshtml");
        var controller = Read("Proyecto_Final", "Controllers", "SellerOrdersController.cs");

        Assert.Contains("@Url.Action(\"Suggestion\", \"SellerOrders\")", view, StringComparison.Ordinal);
        Assert.Contains("id=\"crossSellPopup\"", view, StringComparison.Ordinal);
        Assert.Contains("dismissedForOrder = true;", view, StringComparison.Ordinal);
        Assert.Contains("if (dismissedForOrder || !navigator.onLine", view, StringComparison.Ordinal);
        Assert.Contains("_crossSell.GetSuggestionsAsync(", controller, StringComparison.Ordinal);

        var action = typeof(Proyecto_Final.Controllers.SellerOrdersController).GetMethod("Suggestion");
        Assert.NotNull(action);
        Assert.NotNull(action!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), true).SingleOrDefault());
    }

    private static Proyecto_Final.Models.Store.CrossSellSuggestion Sugerencia(int id, string nombre, string categoria, int stock = 10) =>
        new(id, nombre, categoria, 1000m, stock, string.Empty, "Se compró junto con productos del carrito en 3 pedidos.");

    private static string Read(params string[] parts) => MobileCorreccionesTests.Read(parts);
}
