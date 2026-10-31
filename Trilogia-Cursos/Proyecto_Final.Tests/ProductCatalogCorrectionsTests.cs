using System.Text.RegularExpressions;

namespace Proyecto_Final.Tests;

/// <summary>Correcciones del bloque Productos, promociones y combos (QA G8).</summary>
public sealed class ProductCatalogCorrectionsTests
{
    [Fact]
    public void Migration0210_AcceptsEveryParameterSentWhenSavingProducts()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "database", "migrations", "0210_product_save_procedures.sql"));
        var service = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Services", "AdminDbService.cs"));

        foreach (var procedure in new[] { "sp_Admin_CreateProduct", "sp_Admin_UpdateProduct" })
        {
            var definition = ProcedureDefinition(migration, procedure);
            foreach (var parameter in ParametersSentTo(service, procedure))
                Assert.Matches(new Regex($@"{Regex.Escape(parameter)}\s+(INT|BIT|DECIMAL|NVARCHAR)"), definition);
        }

        // CategoriaId es NOT NULL: la creación debe llenarlo.
        Assert.Contains("CategoriaId", ProcedureDefinition(migration, "sp_Admin_CreateProduct"), StringComparison.Ordinal);
        Assert.Contains("THROW", File.ReadAllText(Path.Combine(root, "database", "migrations", "0210_product_save_procedures.verify.sql")), StringComparison.Ordinal);
    }

    private static string ProcedureDefinition(string migration, string procedure)
    {
        var start = migration.IndexOf($"CREATE OR ALTER PROCEDURE dbo.{procedure}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"La migración no define {procedure}.");
        var end = migration.IndexOf("END';", start, StringComparison.Ordinal);
        return migration[start..end];
    }

    private static IReadOnlyList<string> ParametersSentTo(string service, string procedure)
    {
        var start = service.IndexOf($"\"dbo.{procedure}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"AdminDbService no invoca {procedure}.");
        var end = service.IndexOf("Execute", start, StringComparison.Ordinal);
        var parameters = Regex.Matches(service[start..end], "AddWithValue\\(\"(@\\w+)\"")
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
