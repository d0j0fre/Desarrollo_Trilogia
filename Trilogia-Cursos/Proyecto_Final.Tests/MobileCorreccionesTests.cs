namespace Proyecto_Final.Tests;

/// <summary>
/// Correcciones del bloque App móvil y notificaciones (QA G8).
/// Contratos baratos entre la API, la base y la aplicación.
/// </summary>
public sealed class MobileCorreccionesTests
{
    [Fact]
    public void BUG_10_06_ElPedidoTomaElEstadoDeSuEntregaAlMarcarlaYAlDespachar()
    {
        var driver = Read("Proyecto_FinalAPI", "Services", "Mobile", "DriverMobileDbService.cs");
        var management = Read("Proyecto_FinalAPI", "Services", "Mobile", "ManagementMobileDbService.cs");

        Assert.Contains("dbo.sp_Pedidos_SincronizarEstadoEntrega", driver, StringComparison.Ordinal);
        Assert.Contains("dbo.sp_Pedidos_SincronizarEstadoEntrega", management, StringComparison.Ordinal);
    }

    [Fact]
    public void BUG_10_06_ElPedidoUsaLosNombresDeEstadoQueYaExistenEnLaBase()
    {
        var migration = Read("database", "migrations", "0220_pedido_estado_desde_entrega.sql");

        // Copia el valor del CHECK de RutaPedidos (EnRuta, Entregado, Fallido):
        // no inventa estados nuevos para el pedido.
        Assert.Contains("SET Estado = rp.EstadoEntrega", migration, StringComparison.Ordinal);
        Assert.Contains("rp.EstadoEntrega IN (N'EnRuta', N'Entregado', N'Fallido')", migration, StringComparison.Ordinal);
        Assert.Contains("p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado')", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void BUG_07_02_LaSincronizacionOfflineEnviaElTokenEnLaCabeceraConfigurada()
    {
        var program = Read("Proyecto_Final", "Program.cs");
        var view = Read("Proyecto_Final", "Views", "SellerOrders", "Index.cshtml");

        Assert.Contains("options.HeaderName = \"X-CSRF-TOKEN\"", program, StringComparison.Ordinal);
        Assert.Contains("'X-CSRF-TOKEN': token", view, StringComparison.Ordinal);
        Assert.DoesNotContain("'RequestVerificationToken': token", view, StringComparison.Ordinal);
    }

    internal static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", .. parts]));
}
