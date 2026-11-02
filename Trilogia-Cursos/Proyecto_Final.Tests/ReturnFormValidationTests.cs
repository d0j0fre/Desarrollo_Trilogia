using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

/// <summary>G2-02: registrar una devolución sin producto muestra «Debe seleccionar un producto.».</summary>
public sealed class ReturnFormValidationTests
{
    [Fact]
    public void SinProductoElMensajeEsElDelModelo()
    {
        var model = new ReturnFormViewModel { ProductoId = null, Cantidad = 1, Motivo = "Botella rota" };
        var errores = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errores, validateAllProperties: true));
        Assert.Equal("Debe seleccionar un producto.", Assert.Single(errores).ErrorMessage);
    }

    [Fact]
    public void UnValorVacioEsNuloYNoUnErrorDeEnlace() =>
        // Un int? acepta «» como null: el enlace del modelo ya no agrega «The value '' is invalid.».
        Assert.True(Nullable.GetUnderlyingType(typeof(ReturnFormViewModel).GetProperty(nameof(ReturnFormViewModel.ProductoId))!.PropertyType) == typeof(int));
}
