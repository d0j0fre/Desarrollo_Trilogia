using Xunit;

namespace Proyecto_Final.Tests;

// POST-qa1-01 (CU-073-02, CU-074-03): al rechazar el Gerente un pedido retenido, el vendedor ve el motivo
// en Mis pedidos y recibe un aviso en Notificaciones (migración 0320).
public class RechazoPedidoAvisoVendedorTests
{
    private static string Migracion() => File.ReadAllText(SourcePath("database", "migrations", "0320_pedido_rechazado_aviso_vendedor.sql"));

    [Fact]
    public void Migration0320_ReturnsTheRejectionReasonInMyOrders()
    {
        var sql = Migracion();

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Seller_GetMyOrders", sql, StringComparison.Ordinal);
        Assert.Contains("p.MotivoRechazo", sql, StringComparison.Ordinal);
        // El motivo ya no se devuelve vacío fijo, como en cu102.
        Assert.DoesNotContain("CAST(N'' AS NVARCHAR(500)) AS MotivoRechazo", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0320_NotifiesTheSellerWhenTheOrderIsRejected()
    {
        var sql = Migracion();

        Assert.Contains("CREATE OR ALTER TRIGGER dbo.TR_Pedidos_NotificarRechazoVendedor", sql, StringComparison.Ordinal);
        Assert.Contains("AFTER UPDATE", sql, StringComparison.Ordinal);
        Assert.Contains("dbo.NotificacionesUsuario", sql, StringComparison.Ordinal);
        Assert.Contains("N'PedidoRechazado'", sql, StringComparison.Ordinal);
        Assert.Contains("i.VendedorUsuarioId", sql, StringComparison.Ordinal);
        // Solo avisa cuando el pedido pasa a Rechazado (no en cada actualización).
        Assert.Contains("ISNULL(d.Estado, N'') <> N'Rechazado'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0320_IsAdditiveAndHasVerifyAndRollback()
    {
        var sql = Migracion();

        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SchemaMigrationHistory", sql, StringComparison.Ordinal);
        Assert.True(File.Exists(SourcePath("database", "migrations", "0320_pedido_rechazado_aviso_vendedor.verify.sql")));
        Assert.True(File.Exists(SourcePath("database", "migrations", "0320_pedido_rechazado_aviso_vendedor.rollback.md")));
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
