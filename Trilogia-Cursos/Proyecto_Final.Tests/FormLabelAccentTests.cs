using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

/// <summary>G2-10: las etiquetas de los formularios de Activos y Reclamos llevan tilde.</summary>
public sealed class FormLabelAccentTests
{
    [Theory]
    [InlineData(typeof(AssetFormViewModel), nameof(AssetFormViewModel.Descripcion), "Descripción")]
    [InlineData(typeof(ReclamoFormViewModel), nameof(ReclamoFormViewModel.Descripcion), "Descripción")]
    [InlineData(typeof(ReclamoFormViewModel), nameof(ReclamoFormViewModel.Categoria), "Categoría")]
    public void LaEtiquetaTieneTilde(Type modelo, string propiedad, string etiqueta) =>
        Assert.Equal(etiqueta, modelo.GetProperty(propiedad)!.GetCustomAttribute<DisplayAttribute>()?.Name);
}
