using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Controllers;
using Proyecto_Final.Filters;

namespace Proyecto_Final.Tests;

/// <summary>G2-03: un activo en mantenimiento se puede marcar como disponible.</summary>
public sealed class AssetMarkAvailableTests
{
    [Fact]
    public void LaAccionEsPostConAntiforgeryYElPermisoDelModulo()
    {
        var accion = typeof(AssetsController).GetMethod(nameof(AssetsController.MarkAvailable))!;
        var permiso = Assert.Single(typeof(AssetsController).GetCustomAttributes(typeof(AdminAuthorizeAttribute), false).Cast<AdminAuthorizeAttribute>());

        Assert.NotNull(accion.GetCustomAttributes(typeof(HttpPostAttribute), false).SingleOrDefault());
        Assert.NotNull(accion.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false).SingleOrDefault());
        Assert.Equal("ACTIVOS_GESTIONAR", permiso.Arguments![1]);
    }

    [Fact]
    public void ElBotonSoloApareceConElActivoEnMantenimiento()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Assets", "Details.cshtml");
        var inicio = vista.IndexOf("@if (Model.Activo.Estado == \"Mantenimiento\")", StringComparison.Ordinal);

        Assert.True(inicio > 0);
        Assert.True(vista.IndexOf("asp-action=\"MarkAvailable\"", StringComparison.Ordinal) > inicio);
    }

    [Fact]
    public void ElProcedimientoSoloAceptaMantenimiento()
    {
        var sql = MobileCorreccionesTests.Read("database", "migrations", "0254_activo_marcar_disponible.sql");

        Assert.Contains("IF @Estado <> N'Mantenimiento'", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE ActivoId = @ActivoId AND Estado = N'Mantenimiento'", sql, StringComparison.Ordinal);
    }
}
