using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;

namespace JJ.Commerce;

public sealed record PricedQuantity(int Product, int Quantity, decimal Price, decimal Discount, string? ProductName = null);
public sealed record QuoteMoney(decimal Gross, decimal Discount, decimal Net, decimal Tax, decimal FinalTotal)
{
    public bool RequiresSupervisor => FinalTotal > 2_000_000m;
}
public sealed record PricedBasket(IReadOnlyList<PricedQuantity> Lines, QuoteMoney Money);
public sealed class CommercialBenefitUnavailableException() : InvalidOperationException(
    "El beneficio requiere un contrato de validación adicional.");

public static class ServerPricing
{
    public const string Contract = "PromotionEngine+0012-aggregate-tax-v1";
    public const decimal MaximumSqlMoney = 9_999_999_999_999_999.99m;

    public static PricedBasket Evaluate(IReadOnlyList<CartItemViewModel> source,
        IReadOnlyList<ActivePromotionViewModel> promotions, string segment, DateTime businessDate)
    {
        if (segment is not ("Minorista" or "Mayorista") || source.Count is < 1 or > 100
            || source.Any(x => x.ItemType != CartItemTypes.Product || x.ComboId is not null || x.EsRegalo
                || x.SubtotalAntesDescuento is not null) || source.Select(x => x.ProductoId).Distinct().Count() != source.Count)
            throw new ArgumentException("El catálogo o la solicitud de precios no está acreditado.");
        // Copy inputs; cart/session state is never an authority for the quote.
        var items = source.Select(x => new CartItemViewModel
        {
            ProductoId = x.ProductoId, Cantidad = x.Cantidad, Precio = x.Precio, Nombre = x.Nombre
        }).OrderBy(x => x.ProductoId).ToList();
        _ = Calculate(items.Select(x => new PricedQuantity(x.ProductoId, x.Cantidad, x.Precio, 0)).ToArray());
        var result = PromotionEngine.Apply(items, promotions, segment, businessDate.Date);
        if (result.Gifts.Count > 0 || result.Applied.Any(x => x.TipoBeneficio != "Descuento"))
            throw new CommercialBenefitUnavailableException();
        var lines = items.Select(x => new PricedQuantity(x.ProductoId, x.Cantidad, x.Precio, x.MontoDescuento, x.Nombre)).ToArray();
        return new(lines, Calculate(lines));
    }

    public static QuoteMoney Calculate(IReadOnlyList<PricedQuantity> lines)
    {
        if (lines.Count is < 1 or > 100 || lines.Select(x => x.Product).Distinct().Count() != lines.Count)
            throw new ArgumentException("Líneas de cotización no válidas.");
        decimal gross = 0, discount = 0;
        foreach (var line in lines)
        {
            if (line.Product <= 0 || line.Quantity is < 1 or > 9999 || !Cents(line.Price) || !Cents(line.Discount))
                throw new ArgumentException("Precio o cantidad fuera del contrato.");
            var subtotal = checked(line.Price * line.Quantity);
            if (line.Discount > subtotal) throw new ArgumentException("El descuento excede el bruto.");
            gross = checked(gross + subtotal); discount = checked(discount + line.Discount);
        }
        var net = gross - discount;
        var tax = Math.Round(checked(net * 0.13m), 2, MidpointRounding.AwayFromZero);
        var final = checked(net + tax);
        if (gross > MaximumSqlMoney || discount > MaximumSqlMoney || final > MaximumSqlMoney)
            throw new ArgumentOutOfRangeException(nameof(lines), "El importe excede decimal(18,2).");
        return new(gross, discount, net, tax, final);
    }

    private static bool Cents(decimal value) => value >= 0 && value <= MaximumSqlMoney
        && decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;
}
