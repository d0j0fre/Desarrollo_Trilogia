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

    private static int CuantasVeces(string texto, string buscado)
    {
        var cuenta = 0;
        for (var i = texto.IndexOf(buscado, StringComparison.Ordinal); i >= 0; i = texto.IndexOf(buscado, i + buscado.Length, StringComparison.Ordinal))
            cuenta++;
        return cuenta;
    }
}
