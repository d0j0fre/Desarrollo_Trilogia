using Moq;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>G2-32: los botones del detalle de un pedido se muestran según el permiso de su acción.</summary>
public sealed class OrderDetailButtonsTests
{
    // Permisos de la base demo (con 0251) que deciden estos botones.
    private static readonly Dictionary<string, string[]> Permisos = new()
    {
        ["Empleado"] = ["PEDIDOS_VER", "PEDIDOS_CAMBIAR_ESTADO", "FACTURACION_VER"],
        ["Gerente"] = ["PEDIDOS_VER", "PEDIDOS_AUTORIZAR_RECHAZAR"],
        ["Bodeguero"] = ["PEDIDOS_VER", "PEDIDOS_PREPARAR"],
        ["Facturador"] = ["PEDIDOS_VER", "FACTURACION_VER"],
    };

    [Theory]
    [InlineData("Empleado", true, true, false)]
    [InlineData("Gerente", false, false, false)]
    [InlineData("Bodeguero", false, false, false)]
    [InlineData("Facturador", false, true, false)]
    [InlineData("Administrador", true, true, true)]
    public async Task CadaPerfilVeSoloLosBotonesQuePuedeUsar(string rol, bool cambiarEstado, bool verFactura, bool facturar)
    {
        var permisos = new Mock<IRolePermissionService>();
        var codigos = Permisos.GetValueOrDefault(rol, []);
        permisos.Setup(p => p.HasCodePermissionAsync(rol, It.IsAny<string>())).ReturnsAsync((string _, string c) => codigos.Contains(c));
        permisos.Setup(p => p.HasModulePermissionAsync(rol, It.IsAny<string>()))
            .ReturnsAsync((string? _, string m) => codigos.Any(c => c.StartsWith(m.ToUpperInvariant() + "_", StringComparison.Ordinal)));
        var menu = new NavigationMenuService(permisos.Object);

        Assert.Equal(cambiarEstado, await menu.CanSubmitAsync(rol, "OrdersAdmin", "UpdateStatus"));
        Assert.Equal(verFactura, await menu.CanOpenAsync(rol, "Billing", "Detail"));
        Assert.Equal(facturar, await menu.CanSubmitAsync(rol, "Billing", "GenerateFromOrder"));
    }

    [Fact]
    public void LaVistaConsultaElPermisoDeCadaBoton()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "OrdersAdmin", "Detail.cshtml");

        Assert.Contains("MenuAccess.CanSubmitAsync(rol, \"OrdersAdmin\", \"UpdateStatus\")", vista, StringComparison.Ordinal);
        Assert.Contains("MenuAccess.CanOpenAsync(rol, \"Billing\", \"Detail\")", vista, StringComparison.Ordinal);
        Assert.Contains("MenuAccess.CanSubmitAsync(rol, \"Billing\", \"GenerateFromOrder\")", vista, StringComparison.Ordinal);
        Assert.Contains("Model.CanGenerateInvoice && puedeFacturar", vista, StringComparison.Ordinal);
    }
}
