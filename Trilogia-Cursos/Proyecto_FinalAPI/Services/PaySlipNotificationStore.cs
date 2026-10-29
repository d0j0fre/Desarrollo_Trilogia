using System.Data;
using JJ.Commerce;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

/// <summary>Restricted SQL authority; never supplies recipient data from an HTTP request.</summary>
public sealed class PaySlipNotificationStore(IConfiguration configuration, TimeProvider clock) : IPaySlipNotificationStore
{
    public async Task<PaySlipNotificationReceipt> RequestAsync(PaySlipNotificationCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var connection = Connection();
        await using var sql = SessionCommand(connection, "Request", command.Session, command.CalculationId, command.OperationId, clock.GetUtcNow().UtcDateTime);
        return await ReadReceipt(sql, command.OperationId, ct);
    }

    public async Task<PaySlipNotificationClaim> ClaimAsync(PaySlipNotificationCommand command, byte[] token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateToken(token);
        await using var connection = Connection();
        await using var sql = SessionCommand(connection, "Claim", command.Session, command.CalculationId, command.OperationId, clock.GetUtcNow().UtcDateTime);
        sql.Parameters.Add("@ClaimToken", SqlDbType.Binary, 32).Value = token;
        await connection.OpenAsync(ct);
        await using var reader = await sql.ExecuteReaderAsync(ct);
        if (reader.FieldCount != 6 || !await reader.ReadAsync(ct)) throw Invalid();
        Guid operation = reader.GetGuid(0);
        bool shouldSend = reader.GetBoolean(1);
        string state = reader.GetString(2);
        long calculation = reader.GetInt64(3);
        var receipt = CheckedReceipt(operation, state, command.OperationId);
        if (calculation != command.CalculationId || state == "Pending") throw Invalid();
        PaySlipNotificationSnapshot? snapshot;
        if (shouldSend)
        {
            if (state != "Sending" || reader.IsDBNull(4) || reader.IsDBNull(5)) throw Invalid();
            snapshot = new(calculation, reader.GetInt32(4), reader.GetString(5));
            PaySlipNotificationSender.ValidateSnapshot(snapshot, command.CalculationId);
        }
        else
        {
            if (!reader.IsDBNull(4) || !reader.IsDBNull(5)) throw Invalid();
            snapshot = null;
        }
        // Observe all server results and late commit failures before releasing a sendable snapshot.
        await DrainSingleRow(reader, ct);
        return new(receipt, shouldSend, snapshot);
    }

    public async Task<PaySlipNotificationReceipt> CompleteAsync(Guid operation, byte[] token, string state, CancellationToken ct)
    {
        ValidateToken(token);
        if (operation == Guid.Empty || state is not ("SmtpAccepted" or "Unknown")) throw Invalid();
        await using var connection = Connection();
        await using var sql = new SqlCommand("PaySlipNotifications.Complete", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 5 };
        sql.Parameters.Add("@OperationId", SqlDbType.UniqueIdentifier).Value = operation;
        sql.Parameters.Add("@ClaimToken", SqlDbType.Binary, 32).Value = token;
        sql.Parameters.Add("@State", SqlDbType.NVarChar, 20).Value = state;
        var result = await ReadReceipt(sql, operation, ct);
        if (result.State != state) throw Invalid();
        return result;
    }

    public async Task<PaySlipNotificationReceipt> ReceiptAsync(PaySlipNotificationReceiptCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var connection = Connection();
        await using var sql = SessionCommand(connection, "Receipt", command.Session, command.CalculationId, command.OperationId, clock.GetUtcNow().UtcDateTime);
        return await ReadReceipt(sql, command.OperationId, ct);
    }

    private SqlConnection Connection()
    {
        if (!configuration.GetValue<bool>("PaySlipNotifications:Enabled")) throw Invalid();
        PaySlipNotificationConfiguration.Validate(configuration);
        return new(PaySlipNotificationConfiguration.Connection(configuration).ConnectionString);
    }

    internal static SqlCommand SessionCommand(SqlConnection connection, string operation, CommerceSession session, long calculation, Guid request, DateTime now)
    {
        if (operation is not ("Request" or "Claim" or "Receipt") || session is null || !session.IsValid(now)
            || calculation <= 0 || request == Guid.Empty) throw Invalid();
        var sql = new SqlCommand("PaySlipNotifications." + operation, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 10 };
        sql.Parameters.Add("@LoginId", SqlDbType.UniqueIdentifier).Value = session.LoginId;
        sql.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = session.UserId;
        sql.Parameters.Add("@PerfilId", SqlDbType.Int).Value = session.ProfileId;
        sql.Parameters.Add("@SecurityStamp", SqlDbType.UniqueIdentifier).Value = session.SecurityStamp;
        sql.Parameters.Add("@BindingHash", SqlDbType.Binary, 32).Value = session.BindingHash;
        sql.Parameters.Add(new SqlParameter("@IssuedUtc", SqlDbType.DateTime2) { Scale = 7, Value = session.IssuedUtc });
        sql.Parameters.Add(new SqlParameter("@ExpiresUtc", SqlDbType.DateTime2) { Scale = 7, Value = session.ExpiresUtc });
        sql.Parameters.Add("@CalculationId", SqlDbType.BigInt).Value = calculation;
        sql.Parameters.Add("@OperationId", SqlDbType.UniqueIdentifier).Value = request;
        return sql;
    }

    private static async Task<PaySlipNotificationReceipt> ReadReceipt(SqlCommand command, Guid expected, CancellationToken ct)
    {
        await command.Connection!.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (reader.FieldCount != 2 || !await reader.ReadAsync(ct)) throw Invalid();
        var receipt = CheckedReceipt(reader.GetGuid(0), reader.GetString(1), expected);
        await DrainSingleRow(reader, ct);
        return receipt;
    }

    private static PaySlipNotificationReceipt CheckedReceipt(Guid operation, string state, Guid expected)
    {
        var result = new PaySlipNotificationReceipt(operation, state);
        if (!result.IsValid || operation != expected) throw Invalid();
        return result;
    }

    private static async Task DrainSingleRow(SqlDataReader reader, CancellationToken ct)
    {
        if (await reader.ReadAsync(ct) || await reader.NextResultAsync(ct)) throw Invalid();
    }

    private static void ValidateToken(byte[] token)
    {
        if (token is not { Length: 32 } || !token.Any(x => x != 0)) throw Invalid();
    }

    private static InvalidOperationException Invalid() => new("Pay slip notification contract unavailable.");
}
