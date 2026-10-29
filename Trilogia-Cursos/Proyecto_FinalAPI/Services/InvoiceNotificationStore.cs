using System.Data;
using JJ.Commerce;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public sealed class InvoiceNotificationStore(IConfiguration configuration) : IInvoiceNotificationStore
{
    public async Task<InvoiceNotificationReceipt> RequestAsync(InvoiceNotificationCommand value, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = SessionCommand(connection, "Request", value.Session, value.InvoiceId, value.OperationId);
        command.Parameters.Add("@IsInitial", SqlDbType.Bit).Value = value.IsInitial;
        return await Receipt(command, ct);
    }

    public async Task<InvoiceNotificationClaim> ClaimAsync(InvoiceNotificationCommand value, byte[] token, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = SessionCommand(connection, "Claim", value.Session, value.InvoiceId, value.OperationId);
        command.Parameters.Add("@ClaimToken", SqlDbType.Binary, 32).Value = token;
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw Invalid();
        var receipt = CheckedReceipt(reader.GetGuid(0), reader.GetString(2), value.OperationId);
        var send = reader.GetBoolean(1);
        var snapshot = send ? new InvoiceNotificationSnapshot(reader.GetString(3), reader.GetInt32(4), reader.GetString(5),
            reader.GetString(6), DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc), reader.GetDecimal(8)) : null;
        // Never send while a reader/transaction is still producing rows, or before a late SQL error is observed.
        await Drain(reader, ct);
        return new(receipt, send, snapshot);
    }

    public async Task<InvoiceNotificationReceipt> CompleteAsync(Guid operation, byte[] token, string state, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = new SqlCommand("InvoiceNotifications.Complete", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 5 };
        command.Parameters.Add("@OperationId", SqlDbType.UniqueIdentifier).Value = operation;
        command.Parameters.Add("@ClaimToken", SqlDbType.Binary, 32).Value = token;
        command.Parameters.Add("@State", SqlDbType.NVarChar, 20).Value = state;
        return await Receipt(command, ct);
    }

    public async Task<InvoiceNotificationReceipt> ReceiptAsync(InvoiceNotificationReceiptCommand value, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = SessionCommand(connection, "Receipt", value.Session, value.InvoiceId, value.OperationId);
        return await Receipt(command, ct);
    }

    private SqlConnection Connection() => new(InvoiceNotificationConfiguration.Connection(configuration).ConnectionString);
    internal static SqlCommand SessionCommand(SqlConnection connection, string operation, CommerceSession session, int invoice, Guid request)
    {
        if (operation is not ("Request" or "Claim" or "Receipt")) throw Invalid();
        var command = new SqlCommand("InvoiceNotifications." + operation, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 10 };
        command.Parameters.Add("@LoginId", SqlDbType.UniqueIdentifier).Value = session.LoginId;
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = session.UserId;
        command.Parameters.Add("@PerfilId", SqlDbType.Int).Value = session.ProfileId;
        command.Parameters.Add("@SecurityStamp", SqlDbType.UniqueIdentifier).Value = session.SecurityStamp;
        command.Parameters.Add("@BindingHash", SqlDbType.Binary, 32).Value = session.BindingHash;
        command.Parameters.Add(new SqlParameter("@IssuedUtc", SqlDbType.DateTime2) { Scale = 7, Value = session.IssuedUtc });
        command.Parameters.Add(new SqlParameter("@ExpiresUtc", SqlDbType.DateTime2) { Scale = 7, Value = session.ExpiresUtc });
        command.Parameters.Add("@InvoiceId", SqlDbType.Int).Value = invoice;
        command.Parameters.Add("@OperationId", SqlDbType.UniqueIdentifier).Value = request;
        return command;
    }
    private static async Task<InvoiceNotificationReceipt> Receipt(SqlCommand command, CancellationToken ct)
    {
        await command.Connection!.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw Invalid();
        var actual = reader.GetGuid(0); var state = reader.GetString(1);
        var expected = (Guid)command.Parameters["@OperationId"].Value;
        var receipt = CheckedReceipt(actual, state, command.CommandText.EndsWith(".Receipt", StringComparison.Ordinal) && expected == Guid.Empty ? actual : expected);
        await Drain(reader, ct);
        return receipt;
    }
    private static InvoiceNotificationReceipt CheckedReceipt(Guid operation, string state, Guid expected)
    {
        var receipt = new InvoiceNotificationReceipt(operation, state);
        if (operation != expected || !receipt.IsValid) throw Invalid();
        return receipt;
    }
    private static async Task Drain(SqlDataReader reader, CancellationToken ct)
    {
        while (await reader.ReadAsync(ct)) { }
        while (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) { }
    }
    private static InvalidOperationException Invalid() => new("Invoice notification contract unavailable.");
}
