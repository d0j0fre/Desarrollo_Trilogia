using Microsoft.Data.SqlClient;
using System.Reflection;

namespace Proyecto_Final.Tests;

/// <summary>
/// POST-qa1-07: al registrar en Venta móvil una cantidad mayor al stock, el procedimiento responde con el error 50715
/// («No hay stock suficiente para uno o más productos.») y la pantalla lo reemplazaba por un mensaje genérico.
/// </summary>
public sealed class VentaMovilStockMensajeTests
{
    private static string Controller() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Proyecto_Final", "Controllers", "SellerOrdersController.cs"));

    private static SqlException BusinessSqlError(int number, string message)
    {
        var errorCollection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
        var error = (SqlError)Activator.CreateInstance(typeof(SqlError), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [number, (byte)0, (byte)16, "servidor", message, "procedimiento", 1, (Exception?)null], null)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(errorCollection, [error]);
        return (SqlException)typeof(SqlException).GetMethod("CreateException", BindingFlags.Static | BindingFlags.NonPublic,
            null, [typeof(SqlErrorCollection), typeof(string)], null)!.Invoke(null, [errorCollection, "11.0.0"])!;
    }

    [Fact]
    public void BusinessErrors_ReturnsTheStockMessageFromTheProcedure()
    {
        var sql = BusinessSqlError(50715, "No hay stock suficiente para uno o más productos.");
        var wrapped = new InvalidOperationException(sql.Message, sql);

        Assert.True(Proyecto_Final.Services.BusinessErrors.TryGetMessage(wrapped, out var message));
        Assert.Equal("No hay stock suficiente para uno o más productos.", message);
    }

    [Fact]
    public void CreateOrder_ShowsBusinessErrorsBeforeTheGenericMessage()
    {
        var source = Controller();
        var business = source.IndexOf("BusinessErrors.TryGetMessage(ex, out _)", StringComparison.Ordinal);
        var generic = source.IndexOf("No fue posible registrar el pedido en este momento", StringComparison.Ordinal);

        Assert.True(business > 0 && business < generic);
    }
}
