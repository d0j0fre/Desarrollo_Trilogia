using System.Data;
using System.Globalization;
using System.Text.Json;
using JJ.Commerce;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;

namespace Proyecto_FinalAPI.Services;

public interface ICommercialQuoteAuthority
{
    Task<ServerQuoteResult> IssueAsync(ServerQuoteCommand command, CancellationToken cancellationToken);
}

/// <summary>Runs only in the pricing authority. No fallback to Auth, Issuer or MVC SQL identities.</summary>
public sealed class CommercialQuoteAuthority(IConfiguration configuration, TimeProvider clock) : ICommercialQuoteAuthority
{
    private sealed record Capture(byte[] Fingerprint, byte[] SourceHash, string SourceJson, DateTime Expires,
        long RelationshipRevision, bool Published);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ServerQuoteResult> IssueAsync(ServerQuoteCommand command, CancellationToken ct)
    {
        if (command.Session is null || !command.Session.IsValid(clock.GetUtcNow().UtcDateTime))
            throw new UnauthorizedAccessException("El contexto original no está vigente.");
        if (command.Intent is null) throw new ArgumentException("Falta la intención comercial.");
        var intent = command.Intent.Normalize();
        var capture = await CaptureAsync(command.Session, intent, ct);
        if (!capture.Published)
        {
            var source = QuoteSourceSnapshot.Parse(capture.SourceJson);
            if (source.Revision <= 0 || source.Products.Length != intent.Items.Count
                || !source.Products.Select(x => new OrderQuantity(x.Product, x.Quantity)).SequenceEqual(intent.Items))
                throw new InvalidOperationException("La fuente de productos no coincide con la intención.");
            var priced = ServerPricing.Evaluate(source.Products.Select(x => new CartItemViewModel
                { ProductoId = x.Product, Cantidad = x.Quantity, Precio = x.Price, Nombre = x.Name }).ToArray(),
                source.Promotions, source.Segment, source.EvaluationDate.ToDateTime(TimeOnly.MinValue));
            await PublishAsync(command.Session, intent.RequestId, capture, priced, ct);
        }
        return await ReadAsync(command.Session, intent.RequestId, capture, ct);
    }

    private SqlConnection Connection()
    {
        var value = configuration.GetConnectionString("CommercialPricingAuthority")
            ?? throw new InvalidOperationException("Falta la identidad de precios separada.");
        var builder = new SqlConnectionStringBuilder(value);
        if (builder.IntegratedSecurity || string.IsNullOrWhiteSpace(builder.UserID) || builder.PersistSecurityInfo
            || string.IsNullOrWhiteSpace(builder.Password) || builder.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La identidad de precios no está restringida.");
        return new(builder.ConnectionString);
    }
    private static SqlCommand Command(SqlConnection connection, string operation, CommerceSession session, Guid request)
    {
        var command = new SqlCommand("CommerceQuotes." + operation, connection)
            { CommandType = CommandType.StoredProcedure, CommandTimeout = 10 };
        var p = command.Parameters;
        p.Add("@LoginId", SqlDbType.UniqueIdentifier).Value = session.LoginId;
        p.Add("@UsuarioId", SqlDbType.Int).Value = session.UserId;
        p.Add("@PerfilId", SqlDbType.Int).Value = session.ProfileId;
        p.Add("@SecurityStamp", SqlDbType.UniqueIdentifier).Value = session.SecurityStamp;
        p.Add("@BindingHash", SqlDbType.Binary, 32).Value = session.BindingHash;
        p.Add(new SqlParameter("@IssuedUtc", SqlDbType.DateTime2) { Scale = 7, Value = session.IssuedUtc });
        p.Add(new SqlParameter("@ExpiresUtc", SqlDbType.DateTime2) { Scale = 7, Value = session.ExpiresUtc });
        p.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = request;
        return command;
    }
    private async Task<Capture> CaptureAsync(CommerceSession session, SellerQuoteIntent intent, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = Command(connection, "Capture", session, intent.RequestId);
        var p = command.Parameters;
        p.Add("@Customer", SqlDbType.Int).Value = intent.Customer;
        p.Add("@Delivery", SqlDbType.NVarChar, 50).Value = intent.Delivery;
        p.Add("@Address", SqlDbType.NVarChar, 255).Value = intent.Address;
        p.Add("@Notes", SqlDbType.NVarChar, 255).Value = (object?)intent.Notes ?? DBNull.Value;
        p.Add("@Identification", SqlDbType.NVarChar, 100).Value = (object?)intent.Identification ?? DBNull.Value;
        p.Add("@Channel", SqlDbType.NVarChar, 50).Value = intent.Channel;
        p.Add("@ItemsJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(intent.Items, Json);
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Falta el propietario confirmado de cotización.");
        var capture = new Capture((byte[])reader.GetValue(0), (byte[])reader.GetValue(1), reader.GetString(2),
            DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc), reader.GetInt64(4), reader.GetBoolean(5));
        await Complete(reader, ct);
        if (capture.Fingerprint.Length != 32 || capture.SourceHash.Length != 32 || capture.RelationshipRevision <= 0
            || capture.Expires > session.ExpiresUtc || capture.Expires <= clock.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException("La vigencia o el contexto de cotización no está acreditado.");
        return capture;
    }
    private async Task PublishAsync(CommerceSession session, Guid request, Capture capture, PricedBasket basket, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = Command(connection, "Publish", session, request);
        command.Parameters.Add("@Fingerprint", SqlDbType.Binary, 32).Value = capture.Fingerprint;
        command.Parameters.Add("@SourceHash", SqlDbType.Binary, 32).Value = capture.SourceHash;
        command.Parameters.Add("@Lines", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(basket.Lines.Select(x => new
        {
            product = x.Product, quantity = x.Quantity,
            price = x.Price.ToString("F2", CultureInfo.InvariantCulture),
            discount = x.Discount.ToString("F2", CultureInfo.InvariantCulture)
        }));
        await connection.OpenAsync(ct);
        await command.ExecuteNonQueryAsync(ct);
    }
    private async Task<ServerQuoteResult> ReadAsync(CommerceSession session, Guid request, Capture capture, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = Command(connection, "ReadOwn", session, request);
        command.Parameters.Add("@Fingerprint", SqlDbType.Binary, 32).Value = capture.Fingerprint;
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Falta la cotización persistida.");
        if (reader.GetGuid(0) != request || reader.GetInt64(2) != capture.RelationshipRevision
            || !((byte[])reader.GetValue(3)).SequenceEqual(capture.SourceHash)
            || !((byte[])reader.GetValue(4)).SequenceEqual(capture.Fingerprint))
            throw new InvalidOperationException("La cotización persistida no coincide con su contexto.");
        var expiry = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);
        var money = new QuoteMoney(reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9));
        if (!await reader.NextResultAsync(ct)) throw new InvalidOperationException("Faltan líneas de cotización.");
        var lines = new List<PricedQuantity>();
        while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetString(4)));
        await Complete(reader, ct);
        if (money != ServerPricing.Calculate(lines) || expiry != capture.Expires)
            throw new InvalidOperationException("La paridad monetaria persistida no está acreditada.");
        return new(request, expiry, capture.RelationshipRevision, Convert.ToHexString(capture.SourceHash),
            Convert.ToHexString(capture.Fingerprint), lines, money);
    }
    private static async Task Complete(SqlDataReader reader, CancellationToken ct)
    { while (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) { } }
}
