using Microsoft.AspNetCore.Mvc;
using Moq;
using Proyecto_Final.Controllers;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>G2-40: registrar un movimiento de inventario exige INVENTARIO_MOVIMIENTOS.</summary>
public sealed class InventoryMovementPermissionTests
{
    [Fact]
    public void ElGetYElPostExigenElPermisoDeMovimientos()
    {
        var acciones = typeof(InventoryController).GetMethods().Where(m => m.Name == nameof(InventoryController.RegisterMovement)).ToList();

        Assert.Equal(2, acciones.Count);
        foreach (var accion in acciones)
        {
            var permiso = Assert.Single(accion.GetCustomAttributes(typeof(AdminAuthorizeAttribute), false).Cast<AdminAuthorizeAttribute>());
            Assert.Equal("INVENTARIO_MOVIMIENTOS", permiso.Arguments![1]);
        }
    }

    [Theory]
    [InlineData("Gerente", false)]
    [InlineData("Compras", false)]
    [InlineData("Bodeguero", true)]
    [InlineData("Empleado", true)]
    public async Task ElBotonSoloSeMuestraAQuienTieneElPermiso(string rol, bool puede)
    {
        var permisos = new Mock<IRolePermissionService>();
        permisos.Setup(p => p.HasModulePermissionAsync(rol, "Inventario")).ReturnsAsync(true);
        permisos.Setup(p => p.HasCodePermissionAsync(rol, "INVENTARIO_MOVIMIENTOS")).ReturnsAsync(puede);

        Assert.Equal(puede, await new NavigationMenuService(permisos.Object).CanOpenAsync(rol, "Inventory", "RegisterMovement"));
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Inventory", "Movements.cshtml");
        Assert.Contains("MenuAccess.CanOpenAsync(Context.Session.GetString(\"UserRole\"), \"Inventory\", \"RegisterMovement\")", vista, StringComparison.Ordinal);
    }
}
