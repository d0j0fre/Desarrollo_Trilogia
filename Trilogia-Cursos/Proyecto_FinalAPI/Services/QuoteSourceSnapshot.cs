using System.Text.Json;
using Proyecto_Final.Models.Admin;

namespace Proyecto_FinalAPI.Services;

internal sealed record QuoteSourceProduct(int Product, int Quantity, string Name, decimal Price);
internal sealed record QuoteSourceSnapshot(DateOnly EvaluationDate, string Segment, long Revision, int SourceVersion,
    QuoteSourceProduct[] Products, ActivePromotionViewModel[] Promotions)
{
    internal static QuoteSourceSnapshot Parse(string json)
    {
        QuoteSourceSnapshot? source;
        try { source = JsonSerializer.Deserialize<QuoteSourceSnapshot>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException error) { throw new InvalidOperationException("La fuente de precios no cumple el contrato.", error); }
        if (source is null || source.SourceVersion != 2 || source.Revision <= 0 || source.EvaluationDate == default
            || source.Segment is not ("Minorista" or "Mayorista") || source.Products is null
            || source.Products.Length is < 1 or > 100 || source.Products.Any(x => x is null)
            || source.Promotions is null || source.Promotions.Any(x => x is null))
            throw new InvalidOperationException("La fuente de precios no está acreditada; no se infieren promociones vacías.");
        return source;
    }
}
