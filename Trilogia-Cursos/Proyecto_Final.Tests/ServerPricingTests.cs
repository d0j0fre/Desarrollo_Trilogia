using JJ.Commerce;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;

namespace Proyecto_Final.Tests;

public sealed class ServerPricingTests
{
    [Fact]
    public void Exactly_two_million_is_not_retained_but_a_larger_total_is()
    {
        var exact = ServerPricing.Calculate([new(1, 1, 1_769_911.50m, 0)]);
        Assert.Equal(2_000_000m, exact.FinalTotal);
        Assert.False(exact.RequiresSupervisor);
        var larger = ServerPricing.Calculate([new(1, 2, 1_769_911.50m, 0)]);
        Assert.Equal(3_999_999.99m, larger.FinalTotal);
        Assert.True(larger.RequiresSupervisor);
    }

    [Fact]
    public void Tax_is_rounded_once_over_the_aggregate_net()
    {
        var result = ServerPricing.Calculate([new(1, 1, .05m, 0), new(2, 1, .05m, 0)]);
        Assert.Equal(.01m, result.Tax);
        Assert.Equal(.11m, result.FinalTotal);
        var mixed = ServerPricing.Calculate([new(1, 1, 1_769_911.50m, 0), new(2, 1, .05m, .01m)]);
        Assert.Equal(2_000_000.04m, mixed.FinalTotal);
        Assert.True(mixed.RequiresSupervisor);
    }

    [Fact]
    public void Uses_the_original_engine_priority_and_does_not_mutate_cart_state()
    {
        var day = new DateTime(2026, 10, 19);
        var source = new CartItemViewModel { ProductoId = 1, Cantidad = 3, Precio = .05m };
        ActivePromotionViewModel Promotion(int id, int priority, decimal rate) => new()
        {
            PromocionId = id, Prioridad = priority, ProductoId = 1, Tipo = "DescuentoPorcentual",
            PorcentajeDescuento = rate, CantidadMinima = 1, SegmentoCliente = "Todos",
            FechaInicio = day, FechaFin = day
        };
        var result = ServerPricing.Evaluate([source], [Promotion(1, 9, 1), Promotion(2, 1, 10)], "Minorista", day);
        // A benefit rounded to zero must not consume the product's priority slot.
        Assert.Equal(.02m, result.Money.Discount);
        Assert.Equal(.02m, result.Money.Tax);
        Assert.Equal(.15m, result.Money.FinalTotal);
        Assert.Equal(0, source.MontoDescuento);
    }

    [Fact]
    public void Gifts_are_rejected_instead_of_silently_dropped()
    {
        var day = new DateTime(2026, 10, 19);
        Assert.Throws<CommercialBenefitUnavailableException>(() => ServerPricing.Evaluate(
            [new() { ProductoId = 1, Cantidad = 1, Precio = 1m }],
            [new() { ProductoId = 1, Tipo = "RegaliaPorVolumen", CantidadMinima = 1,
                ProductoRegaloId = 2, CantidadRegalo = 1, ProductoRegaloStock = 1, FechaInicio = day, FechaFin = day }],
            "Minorista", day));
    }

    [Fact]
    public void Rejects_hidden_rounding_duplicate_products_and_out_of_range_totals()
    {
        Assert.Throws<ArgumentException>(() => ServerPricing.Calculate([new(1, 1, .001m, 0)]));
        Assert.Throws<ArgumentException>(() => ServerPricing.Calculate([new(1, 1, 1, 0), new(1, 1, 1, 0)]));
        Assert.Throws<ArgumentException>(() => ServerPricing.Calculate([new(1, 1, 1, 1.01m)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ServerPricing.Calculate([new(1, 2, ServerPricing.MaximumSqlMoney, 0)]));
    }
}
