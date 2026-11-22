using Xunit;

namespace Proyecto_Final.Tests;

// Observaciones 2, 4, 7 y 8 de las pruebas post instalación de qa1 (mejoras chicas).
public class MejorasPostQa1Tests
{
    // Obs. 2: «Total comprado» no cuenta pedidos Rechazados ni Cancelados (migración 0321).
    [Fact]
    public void Migration0321_TotalComprado_ExcludesRejectedAndCancelledOrders()
    {
        var sql = Leer("database", "migrations", "0321_total_comprado_sin_rechazados.sql");

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetClients", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetClientById", sql, StringComparison.Ordinal);
        Assert.Equal(2, Contar(sql, "NOT IN (N'Cancelado', N'Rechazado')"));
        // El conteo de pedidos no cambia.
        Assert.Equal(2, Contar(sql, "COUNT(p.PedidoId) AS TotalPedidos"));
    }

    [Fact]
    public void Migration0321_IsAdditiveAndHasVerifyAndRollback()
    {
        var sql = Leer("database", "migrations", "0321_total_comprado_sin_rechazados.sql");

        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SchemaMigrationHistory", sql, StringComparison.Ordinal);
        Assert.True(File.Exists(Ruta("database", "migrations", "0321_total_comprado_sin_rechazados.verify.sql")));
        Assert.True(File.Exists(Ruta("database", "migrations", "0321_total_comprado_sin_rechazados.rollback.md")));
    }

    // Obs. 4: si la cantidad se ajusta al stock, el aviso del carrito lo dice (al agregar y al actualizar).
    [Fact]
    public void Cart_WarnsWhenQuantityIsAdjustedToStock()
    {
        var cart = Leer("Proyecto_Final", "Controllers", "CartController.cs");

        Assert.Contains("internal static string MensajeAjusteStock", cart, StringComparison.Ordinal);
        Assert.Contains("la cantidad en el carrito se ajustó a", cart, StringComparison.Ordinal);
        // Agregar producto, agregar combo, actualizar combo y actualizar producto.
        Assert.True(Contar(cart, "TempData[\"WarningMessage\"] = MensajeAjusteStock(") >= 4);
        // El aviso de éxito solo sale si no hubo ajuste.
        Assert.Contains("fue agregado al carrito.", cart, StringComparison.Ordinal);
    }

    // Obs. 7: el abono mayor a la deuda muestra el motivo, con tilde.
    [Fact]
    public void Credits_ShowsTheBusinessReasonOfTheRejectedMovement()
    {
        var credits = Leer("Proyecto_Final", "Controllers", "CreditsController.cs");

        Assert.DoesNotContain("movimiento de credito.", credits, StringComparison.Ordinal);
        Assert.Contains("MensajeMovimientoRechazado(ex)", credits, StringComparison.Ordinal);
        Assert.Contains("sql.Number is >= 52000 and <= 52999", credits, StringComparison.Ordinal);
        Assert.Contains("No se pudo registrar el movimiento de crédito.", credits, StringComparison.Ordinal);
        // El motivo viene del procedimiento almacenado.
        Assert.Contains("El monto no puede superar la deuda actual del cliente.", Leer("database", "cu062_creditos_patch.sql"), StringComparison.Ordinal);
    }

    // Obs. 8: Venta móvil sin conexión muestra «Retenido» y el mensaje del servidor, no «Sincronizado».
    [Fact]
    public void OfflineSales_ShowsRetainedOrdersAsRetainedWithServerMessage()
    {
        var vista = Leer("Proyecto_Final", "Views", "SellerOrders", "Index.cshtml");

        Assert.Contains("function isOrderDone(order)", vista, StringComparison.Ordinal);
        Assert.Contains("/retenid/i.test(estadoServidor)", vista, StringComparison.Ordinal);
        Assert.Contains("retenido ? 'Retenido' : 'Sincronizado'", vista, StringComparison.Ordinal);
        Assert.Contains("orders[currentIndex].mensajeServidor", vista, StringComparison.Ordinal);
        // Ningún filtro compara solo contra «Sincronizado»: un pedido Retenido no se reenvía.
        Assert.DoesNotContain("order.estadoOffline !== 'Sincronizado'", vista, StringComparison.Ordinal);
    }

    private static int Contar(string texto, string valor)
    {
        var total = 0;
        for (var i = texto.IndexOf(valor, StringComparison.Ordinal); i >= 0; i = texto.IndexOf(valor, i + valor.Length, StringComparison.Ordinal))
            total++;
        return total;
    }

    private static string Leer(params string[] partes) => File.ReadAllText(Ruta(partes));

    private static string Ruta(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
    }
}
