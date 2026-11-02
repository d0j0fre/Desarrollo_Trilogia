using System.Text.RegularExpressions;
using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

/// <summary>G2-42: filtros de Inventario → Movimientos y textos con doble codificación en los scripts.</summary>
public sealed class InventoryMovementsListTests
{
    // «Ã±», «Ã©», «Â¿»…: UTF-8 leído como Windows-1252 y vuelto a guardar.
    private static readonly Regex DobleCodificacion = new("[ÃÂ][\u0080-¿ŒœŠšŸŽžƒˆ˜–—‘-„†-•…‰‹›€™]");

    [Fact]
    public void LosScriptsDeBaseNoTraenTextoConDobleCodificacion()
    {
        var database = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database");
        // cu096 y 0252 nombran los textos dañados a propósito para repararlos; 0022 ya está aplicada y no se edita.
        var excluidos = new[] { "cu096_corregir_mojibake_productos.sql", "0022_password_hash_transition.sql" };

        var conDano = Directory.EnumerateFiles(database, "*.sql", SearchOption.AllDirectories)
            .Where(f => !excluidos.Contains(Path.GetFileName(f)) && !Path.GetFileName(f).StartsWith("0252_", StringComparison.Ordinal))
            .Where(f => DobleCodificacion.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(conDano);
    }

    [Fact]
    public void LaListaFiltraPorProductoTipoYFechas()
    {
        var parametros = typeof(InventoryController).GetMethod(nameof(InventoryController.Movements))!
            .GetParameters().Select(p => p.Name);
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Inventory", "Movements.cshtml");

        Assert.Equal(new[] { "buscar", "tipo", "desde", "hasta" }, parametros);
        foreach (var campo in new[] { "name=\"buscar\"", "name=\"tipo\"", "name=\"desde\"", "name=\"hasta\"" })
        {
            Assert.Contains(campo, vista, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LaMigracionDeReparacionNoDependeDeSuPropiaCodificacion()
    {
        var sql = MobileCorreccionesTests.Read("database", "migrations", "0252_reparar_texto_mal_codificado.sql");
        var codigo = string.Join('\n', sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        codigo = Regex.Replace(codigo, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        Assert.DoesNotMatch(DobleCodificacion, codigo);
        Assert.Contains("NCHAR(195)", codigo, StringComparison.Ordinal);
        Assert.Contains("CodePage", codigo, StringComparison.Ordinal);
    }
}
