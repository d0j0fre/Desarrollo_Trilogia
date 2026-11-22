using Xunit;

namespace Proyecto_Final.Tests;

// POST-qa3-01..04 (CU-152-01, CU-192-01, CU-211-01, CU-232-01): avisos que no existían y validación del
// odómetro en la app móvil (migraciones 0327 y 0328).
public class AvisosPostQa3Tests
{
    private static string Migracion(string nombre) => File.ReadAllText(SourcePath("database", "migrations", nombre));

    [Fact]
    public void Migration0327_NotifiesTheClientWhenAClaimIsClosed()
    {
        var sql = Migracion("0327_avisos_reclamo_meta_departamento.sql");

        Assert.Contains("CREATE OR ALTER TRIGGER dbo.TR_Reclamos_NotificarCierre", sql, StringComparison.Ordinal);
        Assert.Contains("N'ReclamoCerrado'", sql, StringComparison.Ordinal);
        Assert.Contains("i.UsuarioId", sql, StringComparison.Ordinal);
        // Solo avisa cuando el reclamo pasa a Cerrado.
        Assert.Contains("ISNULL(d.Estado, N'') <> N'Cerrado'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0327_NotifiesTheSellerWhenTheGoalIsSetOrChanged()
    {
        var sql = Migracion("0327_avisos_reclamo_meta_departamento.sql");

        Assert.Contains("CREATE OR ALTER TRIGGER dbo.TR_MetasVendedor_NotificarVendedor", sql, StringComparison.Ordinal);
        Assert.Contains("N'MetaAsignada'", sql, StringComparison.Ordinal);
        Assert.Contains("i.VendedorUsuarioId", sql, StringComparison.Ordinal);
        // No avisa si solo cambian las observaciones: únicamente alta o cambio de monto.
        Assert.Contains("d.MontoMeta <> i.MontoMeta", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0327_NotifiesTheNewChatDepartmentMember()
    {
        var sql = Migracion("0327_avisos_reclamo_meta_departamento.sql");

        Assert.Contains("CREATE OR ALTER TRIGGER dbo.TR_ChatDepartamentoMiembros_NotificarMiembro", sql, StringComparison.Ordinal);
        Assert.Contains("AFTER INSERT", sql, StringComparison.Ordinal);
        Assert.Contains("N'ChatDepartamento'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0328_ValidatesTheOdometerInTheMobileProcedure()
    {
        var sql = Migracion("0328_kilometraje_movil_odometro.sql");

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Chofer_Kilometraje_Abrir", sql, StringComparison.Ordinal);
        Assert.Contains("@KmInicial < @Odometro", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 53046", sql, StringComparison.Ordinal);

        var controller = File.ReadAllText(SourcePath("Proyecto_FinalAPI", "Controllers", "MobileDriverController.cs"));
        Assert.Contains("53046 =>", controller, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0327_avisos_reclamo_meta_departamento")]
    [InlineData("0328_kilometraje_movil_odometro")]
    public void Migrations_AreAdditiveAndHaveVerifyRollbackAndReadmeRow(string nombre)
    {
        var sql = Migracion(nombre + ".sql");

        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SchemaMigrationHistory", sql, StringComparison.Ordinal);
        Assert.True(File.Exists(SourcePath("database", "migrations", nombre + ".verify.sql")));
        Assert.True(File.Exists(SourcePath("database", "migrations", nombre + ".rollback.md")));
        Assert.Contains(nombre + ".sql", Migracion("README.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationSummaryWithoutErrors_IsHiddenInsteadOfAnEmptyRedBar()
    {
        var css = File.ReadAllText(SourcePath("Proyecto_Final", "wwwroot", "css", "sprint4-frontend-polish.css"));
        Assert.Contains(".alert.validation-summary-valid", css, StringComparison.Ordinal);
        Assert.Contains("display: none", css[css.IndexOf(".alert.validation-summary-valid", StringComparison.Ordinal)..], StringComparison.Ordinal);

        // La pantalla de Inteligencia de inventario usa ese resumen de validación.
        var vista = File.ReadAllText(SourcePath("Proyecto_Final", "Views", "InventoryIntelligence", "Index.cshtml"));
        Assert.Contains("asp-validation-summary", vista, StringComparison.Ordinal);
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
    }
}
