using System.Text.Json;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>Correcciones de Rutas y Entregas (BUG-06-04, BUG-10-01, BUG-10-02, BUG-25-01/02/03 y G2/G3 del manual).</summary>
public sealed class RoutesCorreccionesTests
{
    private static string Migracion(string archivo) =>
        MobileCorreccionesTests.Read("database", "migrations", archivo);

    // ── BUG-06-04 · cliente inactivo ────────────────────────────────────────

    [Fact]
    public void Migracion0280_NoOfreceNiAceptaPedidosDeClientesInactivos()
    {
        var sql = Migracion("0280_rutas_cliente_inactivo.sql");

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Rutas_GetAssignableOrders", sql, StringComparison.Ordinal);
        Assert.Contains("AND u.Activo = 1", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 52014", sql, StringComparison.Ordinal);   // AddOrder
        Assert.Contains("THROW 52006", sql, StringComparison.Ordinal);   // Create
    }

    // ── BUG-10-01 · prioridad ───────────────────────────────────────────────

    [Fact]
    public void BuildOrdersJson_IncluyeLaPrioridadElegidaYDejaLosDemasSinDato()
    {
        var json = LogisticsDbService.BuildOrdersJson(
            [47, 48, 47, 50],
            new Dictionary<int, int> { [47] = RoutePriority.Urgente, [48] = RoutePriority.Alta });

        var items = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

        Assert.Equal(3, items.Count);   // 47 no se repite
        Assert.Equal(1, items[0].GetProperty("prioridad").GetInt32());
        Assert.Equal(2, items[1].GetProperty("prioridad").GetInt32());
        Assert.False(items[2].TryGetProperty("prioridad", out _));   // sin dato: el procedimiento usa Normal
    }

    [Theory]
    [InlineData(1, "Urgente")]
    [InlineData(2, "Alta")]
    [InlineData(3, "Normal")]
    [InlineData(0, "Normal")]
    public void RoutePriority_Texto(int valor, string esperado) =>
        Assert.Equal(esperado, RoutePriority.Texto(valor));

    [Fact]
    public void Migracion0281_SecuenciaPorPrioridadYDespuesPorCercania()
    {
        var sql = Migracion("0281_rutas_prioridad_secuenciacion.sql");

        // Las dos secuenciaciones (automática y al recalcular) usan la misma regla.
        Assert.Equal(2, CuantasVeces(sql, "SELECT @Nivel = MIN(Prioridad) FROM @Stops WHERE Visitado = 0 AND TieneCoord = 1;"));
        Assert.Equal(2, CuantasVeces(sql, "AND Prioridad = @Nivel"));
        Assert.Contains("CHECK (Prioridad BETWEEN 1 AND 3)", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 54002", sql, StringComparison.Ordinal);
        // Prioridad es la última columna de sp_Rutas_GetOrders: los lectores por posición no se alteran.
        Assert.Matches(@"rp\.Longitud,\s+rp\.Prioridad", sql);
    }

    // ── BUG-25-01 · secuenciar exige coordenadas ────────────────────────────

    [Fact]
    public void Migracion0282_RechazaSecuenciarSinCoordenadasAntesDeOrdenar()
    {
        var sql = Migracion("0282_rutas_secuenciar_exige_ubicacion.sql");

        var rechazo = sql.IndexOf("THROW 54012", StringComparison.Ordinal);
        var ordena = sql.IndexOf("WHILE EXISTS", StringComparison.Ordinal);
        var guarda = sql.IndexOf("UPDATE rp", StringComparison.Ordinal);

        Assert.True(rechazo > 0, "Falta el rechazo 54012.");
        Assert.True(rechazo < ordena && ordena < guarda, "La validación debe ir antes de ordenar y de guardar.");
    }

    [Fact]
    public void SecuenciarMuestraElMotivoDelProcedimientoYNoDejaElAvisoDeParadasAlFinal()
    {
        var controlador = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "RoutesAdminController.cs");
        var secuenciar = controlador[controlador.IndexOf("public async Task<IActionResult> Sequence(", StringComparison.Ordinal)..];
        secuenciar = secuenciar[..secuenciar.IndexOf("return RedirectToAction", StringComparison.Ordinal)];

        Assert.Contains("BusinessErrors.MessageOr(ex", secuenciar, StringComparison.Ordinal);
        Assert.DoesNotContain("quedaron al final", secuenciar, StringComparison.Ordinal);
    }

    // ── BUG-25-02 · tiempo total estimado ───────────────────────────────────

    private static RouteOrderItemViewModel Parada(int id, int secuencia, double? lat, double? lng, string estado = "Pendiente") => new()
    {
        RutaPedidoId = id,
        PedidoId = id,
        Secuencia = secuencia,
        EstadoEntrega = estado,
        Latitud = (decimal?)lat,
        Longitud = (decimal?)lng
    };

    [Fact]
    public void Estimate_SumaTramosYTiempoDeAtencionEnElOrdenDeSecuencia()
    {
        // Dos paradas sobre el mismo meridiano, ~11,1 km y ~22,2 km desde la bodega (9,9333 / -84,0833).
        var estimacion = RouteEstimator.Estimate(
        [
            Parada(2, 2, 10.1333, -84.0833),
            Parada(1, 1, 10.0333, -84.0833)
        ]);

        Assert.Equal(2, estimacion.Paradas);
        Assert.Equal(0, estimacion.ParadasSinUbicacion);
        // 22,2 km en línea recta × 1,3 = 28,9 km → 57,7 min de conducción + 2 × 10 min de atención.
        Assert.InRange(estimacion.DistanciaKm, 28.5, 29.3);
        Assert.InRange(estimacion.MinutosTotales, 76, 79);
        // La primera parada en secuencia llega antes que la segunda.
        Assert.True(estimacion.PorParada[1].MinutosAcumulados < estimacion.PorParada[2].MinutosAcumulados);
        Assert.Equal(estimacion.MinutosTotales, estimacion.PorParada[2].MinutosAcumulados);
    }

    [Fact]
    public void Estimate_IgnoraEntregadasYFallidasYCuentaLasQueNoTienenUbicacion()
    {
        var estimacion = RouteEstimator.Estimate(
        [
            Parada(1, 1, 10.0333, -84.0833, "Entregado"),
            Parada(2, 2, 10.0333, -84.0833, "Fallido"),
            Parada(3, 3, 10.0333, -84.0833, "EnRuta"),
            Parada(4, 4, null, null)
        ]);

        Assert.Equal(1, estimacion.Paradas);
        Assert.Equal(1, estimacion.ParadasSinUbicacion);
        Assert.Single(estimacion.PorParada);
        Assert.True(estimacion.PorParada.ContainsKey(3));
    }

    [Fact]
    public void Estimate_SinParadasConUbicacionNoInventaUnTiempo()
    {
        var estimacion = RouteEstimator.Estimate([Parada(1, 1, null, null)]);

        Assert.False(estimacion.TieneEstimacion);
        Assert.Equal("—", estimacion.TextoTotal);
    }

    [Theory]
    [InlineData(45, "45 min")]
    [InlineData(60, "1 h")]
    [InlineData(72, "1 h 12 min")]
    public void FormatMinutes(int minutos, string esperado) =>
        Assert.Equal(esperado, RouteEstimator.FormatMinutes(minutos));

    [Fact]
    public void ElDetalleDeLaRutaMuestraElTiempoTotalYLaLlegadaPorParada()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "RoutesAdmin", "Detail.cshtml");

        Assert.Contains("Tiempo total estimado", vista, StringComparison.Ordinal);
        Assert.Contains("Model.Estimacion.TextoTotal", vista, StringComparison.Ordinal);
        Assert.Contains("Llegada estimada", vista, StringComparison.Ordinal);
    }

    // ── BUG-25-03 · exportar la hoja de ruta ────────────────────────────────

    [Fact]
    public void ElDetalleDeLaRutaExportaLaHojaDeRutaConLaLibreriaXlsxDelNavegador()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "RoutesAdmin", "Detail.cshtml");

        Assert.Contains("id=\"exportRouteXlsx\"", vista, StringComparison.Ordinal);
        Assert.Contains("~/js/xlsx-export.js", vista, StringComparison.Ordinal);
        Assert.Contains("window.XlsxExport.download(hoja.archivo", vista, StringComparison.Ordinal);
        // El nombre del archivo lleva el código de la ruta y termina en .xlsx.
        Assert.Contains("hoja-de-ruta-{c.Codigo}.xlsx", vista, StringComparison.Ordinal);
    }

    // ── BUG-10-02 · el tablero lista los pedidos ────────────────────────────

    [Fact]
    public void Migracion0283_ListaPedidosEnRutaYPendientesDeEntrega()
    {
        var sql = Migracion("0283_tablero_entregas_pedidos.sql");

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Entregas_PedidosTablero", sql, StringComparison.Ordinal);
        Assert.Contains("CAST(N'EnRuta' AS NVARCHAR(10)) AS Grupo", sql, StringComparison.Ordinal);
        Assert.Contains("CAST(N'Pendiente' AS NVARCHAR(10)) AS Grupo", sql, StringComparison.Ordinal);
        // Solo lectura: no escribe en ninguna tabla.
        Assert.DoesNotMatch(@"(?im)^\s*(INSERT|UPDATE|DELETE)\s+(INTO\s+)?dbo\.(?!SchemaMigrationHistory)", sql);
    }

    [Fact]
    public void ElTableroMuestraLasListasDePedidosEnRutaYPendientesYLasRefrescaConElSnapshot()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "DeliveryBoard", "Index.cshtml");

        Assert.Contains("Pedidos en ruta", vista, StringComparison.Ordinal);
        Assert.Contains("Pendientes de entrega", vista, StringComparison.Ordinal);
        Assert.Contains("Model.EnRuta", vista, StringComparison.Ordinal);
        Assert.Contains("Model.Pendientes", vista, StringComparison.Ordinal);
        Assert.Contains("data.enRuta", vista, StringComparison.Ordinal);
        Assert.Contains("data.pendientes", vista, StringComparison.Ordinal);
    }

    [Fact]
    public void ElControladorDelTableroArmaLasListasConLosPedidosDelProcedimiento()
    {
        var controlador = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "DeliveryBoardController.cs");

        Assert.Contains("GetBoardOrdersAsync", controlador, StringComparison.Ordinal);
        Assert.Contains("EntregasPendientes = pendientes.Length", controlador, StringComparison.Ordinal);
        Assert.Contains("PedidosEnRuta = enRuta.Length", controlador, StringComparison.Ordinal);
    }

    // ── Mensajes del manual (G2-12, G3-08, G3-09) ───────────────────────────

    private static string Controlador(string nombre) =>
        MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", nombre);

    // El rechazo de negocio del procedimiento se muestra tal cual: ningún bloque lo reemplaza por el texto genérico.
    private const string GenericoSolo = @"(=|AddModelError\(string\.Empty,)\s*""No fue posible completar la operación solicitada\.""";

    [Fact]
    public void RutasMuestraElMotivoDeNegocioDelProcedimientoEnTodasLasAcciones_G2_12()
    {
        var controlador = Controlador("RoutesAdminController.cs");

        Assert.DoesNotMatch(GenericoSolo, controlador);
        // Crear, agregar, quitar, despachar, cancelar, secuenciar, guardar orden, recalcular y liquidar.
        Assert.Equal(9, CuantasVeces(controlador, "BusinessErrors.MessageOr(ex, \"No fue posible completar la operación solicitada.\")"));
    }

    [Fact]
    public void KilometrajeMuestraElMotivoDelProcedimientoAlAbrirYCerrarLaJornada_G3_08()
    {
        var controlador = Controlador("FleetController.cs");
        var kilometraje = controlador[controlador.IndexOf("public async Task<IActionResult> OpenMileage(", StringComparison.Ordinal)..];
        kilometraje = kilometraje[..kilometraje.IndexOf("// ── CU-153 Mantenimiento", StringComparison.Ordinal)];

        Assert.DoesNotMatch(GenericoSolo, kilometraje);
        Assert.Equal(2, CuantasVeces(kilometraje, "BusinessErrors.MessageOr(ex,"));
    }

    [Fact]
    public void MisEntregasMuestraElMotivoDelProcedimientoAlCambiarElEstado_G3_09()
    {
        var controlador = Controlador("DriverDeliveriesController.cs");

        Assert.Contains("BusinessErrors.MessageOr(exception, \"No fue posible aplicar el estado solicitado.\")", controlador, StringComparison.Ordinal);
        Assert.DoesNotMatch("message = \"No fue posible aplicar el estado solicitado\\.\"", controlador);
    }

    // ── Pendiente heredado: el sitio web también sincroniza el estado del pedido (0220) ─

    [Fact]
    public void DespacharYRecalcularDesdeLaWebSincronizanElEstadoDeLosPedidos()
    {
        var controlador = Controlador("RoutesAdminController.cs");

        Assert.Contains("_logistics.SyncOrderStatusFromDeliveryAsync(rutaId, null)", controlador, StringComparison.Ordinal);
        var despachar = controlador[controlador.IndexOf("public async Task<IActionResult> Dispatch(", StringComparison.Ordinal)..];
        despachar = despachar[..despachar.IndexOf("public async Task<IActionResult> Cancel(", StringComparison.Ordinal)];
        Assert.Contains("SyncOrderStatusAsync(rutaId)", despachar, StringComparison.Ordinal);

        var recalcular = controlador[controlador.IndexOf("public async Task<IActionResult> Recalculate(", StringComparison.Ordinal)..];
        recalcular = recalcular[..recalcular.IndexOf("public async Task<IActionResult> Liquidate(", StringComparison.Ordinal)];
        Assert.Contains("SyncOrderStatusAsync(rutaId)", recalcular, StringComparison.Ordinal);
    }

    [Fact]
    public void MarcarUnaEntregaDesdeMisEntregasSincronizaElPedidoConElProcedimientoDeLa0220()
    {
        var controlador = Controlador("DriverDeliveriesController.cs");
        var servicio = MobileCorreccionesTests.Read("Proyecto_Final", "Services", "LogisticsDbService.cs");

        Assert.Contains("_logistics.SyncOrderStatusFromDeliveryAsync(null, result.RutaPedidoId)", controlador, StringComparison.Ordinal);
        Assert.Contains("dbo.sp_Pedidos_SincronizarEstadoEntrega", servicio, StringComparison.Ordinal);
    }

    // ── G2-13 · contraste en el encabezado oscuro de Rutas ──────────────────

    [Fact]
    public void LaInsigniaDeEstadoDeLaRutaSobreElEncabezadoOscuroEsSolida_G2_13()
    {
        var css = MobileCorreccionesTests.Read("Proyecto_Final", "wwwroot", "css", "design-system-buttons.css");
        var inicio = css.IndexOf(".admin-page-header .admin-badge-soft.warning,", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Falta la regla de la insignia de estado en el encabezado.");
        var regla = css[inicio..css.IndexOf('}', inicio)];

        Assert.Contains("background-color: var(--brand-gold-primary, #D4AF37) !important;", regla, StringComparison.Ordinal);
        Assert.Contains("color: var(--brand-black, #1F1F1F) !important;", regla, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Detail.cshtml", "Volver")]
    [InlineData("Evidences.cshtml", "Volver a la ruta")]
    public void LosBotonesVolverDeRutasUsanElEstiloNeutroQueSeLeeSobreElEncabezado_G2_13(string vista, string texto)
    {
        var html = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "RoutesAdmin", vista);

        Assert.Matches(@"class=""btn btn-ds-neutral[^""]*""><i class=""fa fa-arrow-left mr-2""></i>" + texto + "</a>", html);
    }

    private static int CuantasVeces(string texto, string buscado)
    {
        var cuenta = 0;
        for (var i = texto.IndexOf(buscado, StringComparison.Ordinal); i >= 0; i = texto.IndexOf(buscado, i + buscado.Length, StringComparison.Ordinal))
            cuenta++;
        return cuenta;
    }
}
