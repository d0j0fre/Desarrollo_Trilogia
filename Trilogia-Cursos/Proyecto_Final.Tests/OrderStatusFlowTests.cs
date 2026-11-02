using System.Text.RegularExpressions;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>G2-30: la pantalla de Pedidos ofrece los cambios que acepta sp_Admin_UpdateOrderStatus.</summary>
public sealed class OrderStatusFlowTests
{
    [Fact]
    public void LasTransicionesSonLasDelProcedimiento()
    {
        // Definición vigente de sp_Admin_UpdateOrderStatus (migración 0012).
        var sql = MobileCorreccionesTests.Read("database", "migrations", "0012_inventory_combos_transformations_intelligence.sql");
        var reglas = Regex.Matches(sql, @"\(@EstadoActual = N'(\w+)' AND @NuevoEstado (?:IN \(([^)]*)\)|= N'(\w+)')\)")
            .ToDictionary(
                m => m.Groups[1].Value,
                m => m.Groups[2].Success
                    ? Regex.Matches(m.Groups[2].Value, @"N'(\w+)'").Select(x => x.Groups[1].Value).ToArray()
                    : [m.Groups[3].Value]);

        Assert.Equal(6, reglas.Count);
        foreach (var (actual, siguientes) in reglas)
        {
            Assert.Equal(siguientes, OrderStatusFlow.NextStates(actual, hasInvoice: false));
        }
        foreach (var estado in OrderStatusFlow.All.Select(s => s.Code).Where(c => !reglas.ContainsKey(c)))
        {
            Assert.Empty(OrderStatusFlow.NextStates(estado, hasInvoice: false));
        }
    }

    [Theory]
    [InlineData("Pendiente", "Entregado")]
    [InlineData("Liberado", "Entregado")]
    [InlineData("Entregado", "")]
    [InlineData("EnRuta", "")]
    [InlineData("Fallido", "")]
    [InlineData("Cancelado", "")]
    public void UnPedidoFacturadoSoloPuedePasarAEntregado(string actual, string esperado) =>
        Assert.Equal(esperado, string.Join(",", OrderStatusFlow.NextStates(actual, hasInvoice: true)));

    [Theory]
    [InlineData("EnProceso", "En proceso")]
    [InlineData("EnRuta", "En ruta")]
    [InlineData("Fallido", "No entregado")]
    [InlineData("Pendiente", "Pendiente")]
    [InlineData("Desconocido", "Desconocido")]
    public void LosEstadosSeMuestranLegibles(string codigo, string nombre) =>
        Assert.Equal(nombre, OrderStatusFlow.Name(codigo));

    [Fact]
    public void ElFiltroYElDetalleUsanTodosLosEstados()
    {
        var lista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "OrdersAdmin", "Index.cshtml");
        var detalle = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "OrdersAdmin", "Detail.cshtml");

        Assert.Equal(12, OrderStatusFlow.All.Count);
        Assert.Contains("OrderStatusFlow.All", lista, StringComparison.Ordinal);
        Assert.Contains("OrderStatusFlow.NextStates(Model.Estado, Model.HasInvoice)", detalle, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"Pendiente\"", detalle, StringComparison.Ordinal);
    }
}
