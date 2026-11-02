using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>G2-41: la causa del rechazo de un movimiento y el ajuste a 0.</summary>
public sealed class InventoryMovementRulesTests
{
    [Theory]
    [InlineData("Ajuste", 0, true)]
    [InlineData("Ajuste", 15, true)]
    [InlineData("Entrada", 0, false)]
    [InlineData("Salida", 0, false)]
    [InlineData("Salida", 1, true)]
    [InlineData("Salida", -1, false)]
    public void UnAjustePuedeDejarElStockEnCero(string tipo, int cantidad, bool valido)
    {
        var model = new InventoryMovementFormViewModel { ProductoId = 1, TipoMovimiento = tipo, Cantidad = cantidad };
        var errores = new List<ValidationResult>();

        Assert.Equal(valido, Validator.TryValidateObject(model, new ValidationContext(model), errores, validateAllProperties: true));
    }

    [Fact]
    public void UnaReglaDelProyectoSeMuestraYUnaDeLaLibreriaNo()
    {
        var propia = Assert.Throws<InvalidOperationException>(() => NavigationMenu.GetScreenAccess("NoExiste", "Index"));
        var ajena = Assert.Throws<InvalidOperationException>(() => new List<int>().First());

        Assert.True(BusinessErrors.IsApplicationRule(propia));
        Assert.False(BusinessErrors.IsApplicationRule(ajena));
    }

    [Fact]
    public void ElControladorMuestraLaCausaEnVezDelMensajeGenerico()
    {
        var controller = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "InventoryController.cs");

        Assert.Contains("catch (InvalidOperationException ex) when (BusinessErrors.IsApplicationRule(ex))", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("No se pudo registrar el movimiento de inventario. Revise los datos", controller, StringComparison.Ordinal);
    }
}
