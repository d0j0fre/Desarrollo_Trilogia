using System.Reflection;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>Mensajes de negocio de los procedimientos (G2-01, G2-31, G2-41, G3-10).</summary>
public sealed class BusinessErrorsTests
{
    [Fact]
    public void UnThrowDeNegocioMuestraElTextoDelProcedimiento() =>
        Assert.Equal("El pedido indicado no existe.",
            BusinessErrors.MessageOr(SqlErrors.Create(53013, "El pedido indicado no existe."), "Genérico"));

    [Fact]
    public void UnThrowDeNegocioEnvueltoTambienSeMuestra() =>
        Assert.Equal("Stock insuficiente para Ron.",
            BusinessErrors.MessageOr(new InvalidOperationException("x", SqlErrors.Create(50735, "Stock insuficiente para Ron.")), "Genérico"));

    [Theory]
    [InlineData(547)]   // llave foránea
    [InlineData(2627)]  // llave duplicada
    [InlineData(49999)]
    public void UnErrorTecnicoMantieneElMensajeGenerico(int number) =>
        Assert.Equal("Genérico", BusinessErrors.MessageOr(SqlErrors.Create(number, "Detalle técnico interno"), "Genérico"));

    [Fact]
    public void OtraExcepcionMantieneElMensajeGenerico() =>
        Assert.Equal("Genérico", BusinessErrors.MessageOr(new TimeoutException("socket"), "Genérico"));

    [Theory]
    [InlineData("ReturnsController.cs", 3)]
    [InlineData("AssetsController.cs", 4)] // 3 del G2-01 + «Marcar como disponible» (G2-03)
    [InlineData("ComodatosController.cs", 2)]
    [InlineData("PromotionsController.cs", 4)]
    [InlineData("ReclamosController.cs", 2)]
    [InlineData("ManagerOrdersController.cs", 2)]
    public void G2_01_LosControladoresUsanElMensajeDeNegocio(string controller, int usos)
    {
        var source = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", controller);

        Assert.Equal(usos, source.Split("BusinessErrors.MessageOr(").Length - 1);
        Assert.DoesNotContain("= \"No fue posible completar la operación solicitada.\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("(string.Empty, \"No fue posible completar la operación solicitada.\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void G3_10_LaSolicitudDeGarantiaMuestraLaReglaDelProcedimiento()
    {
        const string generico = "No fue posible registrar la solicitud de garantía en este momento.";
        var source = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "ClientPortalController.cs");

        // El catch del POST Warranty pasa la excepción por el helper común en vez de fijar el genérico.
        Assert.Contains($"BusinessErrors.MessageOr(exception, \"{generico}\")", source, StringComparison.Ordinal);
        Assert.Equal(1, source.Split(generico).Length - 1);
        Assert.Equal("Ya existe una solicitud abierta para este producto.",
            BusinessErrors.MessageOr(SqlErrors.Create(53302, "Ya existe una solicitud abierta para este producto."), generico));
        Assert.Equal(generico, BusinessErrors.MessageOr(SqlErrors.Create(1205, "Deadlock"), generico));
    }
}

/// <summary>SqlException no tiene constructor público: se arma como lo hace el proveedor.</summary>
internal static class SqlErrors
{
    public static SqlException Create(int number, string message)
    {
        var errorCtor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(c => c.GetParameters().Length).First();
        var args = errorCtor.GetParameters().Select(p => p.Name switch
        {
            "infoNumber" => number,
            "errorMessage" => message,
            _ when p.ParameterType == typeof(byte) => (byte)16,
            _ when p.ParameterType == typeof(int) => 0,
            _ when p.ParameterType == typeof(uint) => 0u,
            _ when p.ParameterType == typeof(string) => "prueba",
            _ => (object?)null,
        }).ToArray();
        var error = (SqlError)errorCtor.Invoke(args);

        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(collection, [error]);
        var create = typeof(SqlException).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .First(m => m.Name == "CreateException" && m.GetParameters() is [{ ParameterType: var c }, { ParameterType: var s }]
                        && c == typeof(SqlErrorCollection) && s == typeof(string));
        return (SqlException)create.Invoke(null, [collection, "16.0"])!;
    }
}
