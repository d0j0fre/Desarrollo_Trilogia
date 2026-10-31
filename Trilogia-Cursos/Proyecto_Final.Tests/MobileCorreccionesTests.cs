using Moq;

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

    [Fact]
    public void BUG_08_01_y_10_08_AsignarOReasignarElChoferDejaUnAvisoParaElChoferNuevo()
    {
        var migration = Read("database", "migrations", "0221_notificaciones_usuario.sql");

        Assert.Contains("CREATE OR ALTER TRIGGER dbo.TR_Rutas_NotificarChofer", migration, StringComparison.Ordinal);
        Assert.Contains("AFTER INSERT, UPDATE", migration, StringComparison.Ordinal);
        Assert.Contains("ISNULL(d.ChoferUsuarioId, 0) <> i.ChoferUsuarioId", migration, StringComparison.Ordinal);
        Assert.Contains("N'RutaAsignada'", migration, StringComparison.Ordinal);
        Assert.Contains("N'RutaReasignada'", migration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BUG_10_08_LaBandejaMovilEsLaDelUsuarioDelToken()
    {
        var service = new Moq.Mock<Proyecto_FinalAPI.Services.Mobile.INotificationsMobileDbService>();
        service.Setup(s => s.GetAsync(36, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Proyecto_FinalAPI.Models.MobileNotificationsResponse { NoLeidas = 1 });
        var controller = new Proyecto_FinalAPI.Controllers.MobileNotificationsController(
            service.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Proyecto_FinalAPI.Controllers.MobileNotificationsController>.Instance)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim("sub", "36")], "Bearer"))
                }
            }
        };

        var result = await controller.List(CancellationToken.None);
        await controller.MarkAllRead(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        service.Verify(s => s.GetAsync(36, It.IsAny<CancellationToken>()), Moq.Times.Once);
        service.Verify(s => s.MarkReadAsync(36, null, It.IsAny<CancellationToken>()), Moq.Times.Once);
    }

    [Fact]
    public void BUG_08_01_LaWebMuestraLaCampanaAlPersonal()
    {
        var layout = Read("Proyecto_Final", "Views", "Shared", "_Layout.cshtml");
        var controller = Read("Proyecto_Final", "Controllers", "NotificationsController.cs");

        Assert.Contains("asp-controller=\"Notifications\"", layout, StringComparison.Ordinal);
        Assert.Contains("[SessionAuthorize]", controller, StringComparison.Ordinal);
    }

    internal static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", .. parts]));
}
