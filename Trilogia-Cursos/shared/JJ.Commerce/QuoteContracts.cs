using System.Text.Json.Serialization;
using Proyecto_Final.Validation;

namespace JJ.Commerce;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CommerceSession(Guid LoginId, int UserId, int ProfileId, Guid SecurityStamp,
    byte[] BindingHash, DateTime IssuedUtc, DateTime ExpiresUtc)
{
    public bool IsValid(DateTime now) => LoginId != Guid.Empty && UserId > 0 && ProfileId > 0
        && SecurityStamp != Guid.Empty && BindingHash is { Length: 32 } && BindingHash.Any(x => x != 0)
        && IssuedUtc.Kind == DateTimeKind.Utc && ExpiresUtc.Kind == DateTimeKind.Utc
        && ExpiresUtc - IssuedUtc == TimeSpan.FromHours(8) && IssuedUtc <= now && ExpiresUtc > now;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrderQuantity(int Product, int Quantity);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SellerQuoteIntent(Guid RequestId, int Customer, string Delivery, string Address,
    string? Notes, string? Identification, string Channel, IReadOnlyList<OrderQuantity> Items)
{
    public SellerQuoteIntent Normalize()
    {
        if (RequestId == Guid.Empty || Customer <= 0 || Items is null || Items.Count is < 1 or > 100
            || Items.Any(x => x.Product <= 0 || x.Quantity is < 1 or > 9999)
            || Channel is not ("Venta móvil" or "Venta móvil offline"))
            throw new ArgumentException("La solicitud comercial no es válida.");
        var delivery = Required(Delivery, 50);
        var address = Required(Address, 255);
        var notes = Optional(Notes, 255);
        var identification = CostaRicanIdentificationAttribute.Normalize(Identification);
        if (!new CostaRicanIdentificationAttribute().IsValid(identification))
            throw new ArgumentException("La identificación no cumple el contrato vigente.");
        var items = Items.GroupBy(x => x.Product).OrderBy(x => x.Key)
            .Select(x => new OrderQuantity(x.Key, checked(x.Sum(y => y.Quantity)))).ToArray();
        if (items.Any(x => x.Quantity > 9999)) throw new ArgumentException("La cantidad acumulada excede el contrato.");
        return this with { Delivery = delivery, Address = address, Notes = notes,
            Identification = string.IsNullOrEmpty(identification) ? null : identification, Items = items };
    }
    private static string Required(string? value, int max) => Optional(value, max)
        ?? throw new ArgumentException("Falta un campo comercial obligatorio.");
    private static string? Optional(string? value, int max)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return text?.Length > max ? throw new ArgumentException("Un campo excede el esquema acreditado.") : text;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServerQuoteCommand(CommerceSession Session, SellerQuoteIntent Intent);
public sealed record ServerQuoteResult(Guid RequestId, DateTime ExpiresUtc, long CustomerRelationshipRevision,
    string SourceHash, string IntentFingerprint, IReadOnlyList<PricedQuantity> Lines, QuoteMoney Money);
