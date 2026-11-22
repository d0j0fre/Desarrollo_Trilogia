using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

/// <summary>Ronda 2 (qa2): defectos hallados al repetir los casos en Azure.</summary>
public sealed class PostInstalacionQa2Ronda2Tests
{
    // CU-171-04: una promoción sin producto mostraba «The value '' is invalid.» (error de enlace del entero).
    [Fact]
    public void Promotion_WithoutProduct_ShowsTheSpanishRequiredMessage()
    {
        var model = new PromotionFormViewModel { Nombre = "QA-POST", PorcentajeDescuento = 10, ProductoId = null };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.ErrorMessage == "Seleccione el producto estratégico.");
    }

    [Fact]
    public void Promotion_WithProduct_PassesTheProductValidation()
    {
        var model = new PromotionFormViewModel { Nombre = "QA-POST", PorcentajeDescuento = 10, ProductoId = 5 };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(PromotionFormViewModel.ProductoId)));
    }
}
