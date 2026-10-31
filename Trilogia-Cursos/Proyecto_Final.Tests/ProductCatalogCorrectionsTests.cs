using System.Text.RegularExpressions;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

/// <summary>Correcciones del bloque Productos, promociones y combos (QA G8).</summary>
public sealed class ProductCatalogCorrectionsTests
{
    [Fact]
    public void LatestProductMigrations_AcceptEveryParameterSentWhenSavingProducts()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Services", "AdminDbService.cs"));

        foreach (var procedure in new[] { "sp_Admin_CreateProduct", "sp_Admin_UpdateProduct" })
        {
            var definition = LatestProcedureDefinition(root, procedure);
            foreach (var parameter in ParametersSentTo(service, procedure))
                Assert.Matches(new Regex($@"{Regex.Escape(parameter)}\s+(INT|BIT|DECIMAL|NVARCHAR|DATE)"), definition);
        }

        // CategoriaId es NOT NULL: la creación debe llenarlo (BUG-05-01).
        Assert.Contains("CategoriaId", LatestProcedureDefinition(root, "sp_Admin_CreateProduct"), StringComparison.Ordinal);
        Assert.Contains("THROW", File.ReadAllText(Path.Combine(root, "database", "migrations", "0210_product_save_procedures.verify.sql")), StringComparison.Ordinal);
    }

    [Fact]
    public void LatestProductQueries_ReturnExpirationDate()
    {
        var root = FindRepositoryRoot();
        Assert.Contains("FechaVencimiento", LatestProcedureDefinition(root, "sp_Admin_GetProducts"), StringComparison.Ordinal);
        Assert.Contains("FechaVencimiento", LatestProcedureDefinition(root, "sp_Admin_GetProductById"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(10, true)]
    [InlineData(0, true)]
    [InlineData(-3, true)]
    [InlineData(ProductExpiration.DiasAviso, true)]
    [InlineData(ProductExpiration.DiasAviso + 1, false)]
    public void ProductExpiration_WarnsOnlyWithinThreshold(int dias, bool esperado)
    {
        var hoy = new DateTime(2026, 10, 30);
        Assert.Equal(esperado, ProductExpiration.RequiereAviso(hoy.AddDays(dias), hoy));
    }

    [Fact]
    public void ProductExpiration_WithoutDateNeverWarns()
    {
        var hoy = new DateTime(2026, 10, 30);
        Assert.False(ProductExpiration.RequiereAviso(null, hoy));
        Assert.Null(ProductExpiration.DiasParaVencer(null, hoy));
        Assert.Equal(string.Empty, ProductExpiration.Describir(null, hoy));
        Assert.Contains("vence en 15 día(s)", ProductExpiration.Describir(hoy.AddDays(15), hoy), StringComparison.Ordinal);
        Assert.Contains("venció hace 2 día(s)", ProductExpiration.Describir(hoy.AddDays(-2), hoy), StringComparison.Ordinal);
    }

    /// <summary>Definición del procedimiento en la migración de mayor número que lo redefine.</summary>
    private static string LatestProcedureDefinition(string root, string procedure)
    {
        var header = $"CREATE OR ALTER PROCEDURE dbo.{procedure}\n";
        var migration = Directory.GetFiles(Path.Combine(root, "database", "migrations"), "02*.sql")
            .Where(path => !path.EndsWith(".verify.sql", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => File.ReadAllText(path).Replace("\r\n", "\n"))
            .FirstOrDefault(text => text.Contains(header, StringComparison.Ordinal));
        Assert.True(migration is not null, $"Ninguna migración 02xx define {procedure}.");
        var start = migration!.IndexOf(header, StringComparison.Ordinal);
        var end = migration.IndexOf("END';", start, StringComparison.Ordinal);
        return migration[start..end];
    }

    private static IReadOnlyList<string> ParametersSentTo(string service, string procedure)
    {
        var start = service.IndexOf($"\"dbo.{procedure}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"AdminDbService no invoca {procedure}.");
        var end = service.IndexOf("Execute", start, StringComparison.Ordinal);
        var parameters = Regex.Matches(service[start..end], "\\.Add(?:WithValue)?\\(\"(@\\w+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();
        Assert.NotEmpty(parameters);
        return parameters;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }
}
