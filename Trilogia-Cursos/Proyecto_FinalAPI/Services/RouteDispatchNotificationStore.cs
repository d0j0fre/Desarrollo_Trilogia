using System.Data;
using JJ.Commerce;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public sealed class RouteDispatchNotificationStore(IConfiguration configuration) : IRouteDispatchNotificationStore
{
    public async Task<RouteDispatchNotificationReceipt> RequestAsync(RouteDispatchNotificationCommand value, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = SessionCommand(connection, "Request", value.Session, value.RouteId, value.DispatchId, value.EventId, value.EventGuid, value.OperationId);
        return await Read(command, value.OperationId, value.EventGuid, false, ct);
    }

    public async Task<RouteDispatchNotificationClaim> ClaimAsync(RouteDispatchNotificationCommand value, byte[] token, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = SessionCommand(connection, "Claim", value.Session, value.RouteId, value.DispatchId, value.EventId, value.EventGuid, value.OperationId);
        command.Parameters.Add("@ClaimToken", SqlDbType.Binary, 32).Value = token;
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw Invalid();
        var receipt = Checked(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(3), value.OperationId, value.EventGuid, false);
        bool send = reader.GetBoolean(2);
        var snapshot = send ? new RouteDispatchNotificationSnapshot(reader.GetInt32(4), DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc), reader.GetString(6), reader.GetString(7)) : null;
        await Drain(reader, ct);
        return new(receipt, send, snapshot);
    }

    public async Task<RouteDispatchNotificationReceipt> CompleteAsync(Guid operation, byte[] token, string state, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = new SqlCommand("RouteDispatch.Complete", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 5 };
        command.Parameters.Add("@OperationId", SqlDbType.UniqueIdentifier).Value = operation;
        command.Parameters.Add("@ClaimToken", SqlDbType.Binary, 32).Value = token;
        command.Parameters.Add("@State", SqlDbType.NVarChar, 20).Value = state;
        return await Read(command, operation, Guid.Empty, false, ct);
    }

    public async Task<RouteDispatchNotificationReceipt> ReceiptAsync(RouteDispatchNotificationReceiptCommand value, CancellationToken ct)
    {
        await using var connection = Connection();
        await using var command = SessionCommand(connection, "Receipt", value.Session, value.RouteId, value.DispatchId, value.EventId, value.EventGuid, value.OperationId);
        return await Read(command, value.OperationId, value.EventGuid, true, ct);
    }

    private SqlConnection Connection() => new(RouteDispatchNotificationConfiguration.Connection(configuration).ConnectionString);
    internal static SqlCommand SessionCommand(SqlConnection connection, string operation, CommerceSession session, int route, Guid dispatch, long eventId, Guid eventGuid, Guid request)
    {
        if (operation is not ("Request" or "Claim" or "Receipt")) throw Invalid();
        var command = new SqlCommand("RouteDispatch." + operation, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 10 };
        command.Parameters.Add("@LoginId", SqlDbType.UniqueIdentifier).Value = session.LoginId;
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = session.UserId;
        command.Parameters.Add("@PerfilId", SqlDbType.Int).Value = session.ProfileId;
        command.Parameters.Add("@SecurityStamp", SqlDbType.UniqueIdentifier).Value = session.SecurityStamp;
        command.Parameters.Add("@BindingHash", SqlDbType.Binary, 32).Value = session.BindingHash;
        command.Parameters.Add(new SqlParameter("@IssuedUtc", SqlDbType.DateTime2) { Scale = 7, Value = session.IssuedUtc });
        command.Parameters.Add(new SqlParameter("@ExpiresUtc", SqlDbType.DateTime2) { Scale = 7, Value = session.ExpiresUtc });
        command.Parameters.Add("@RutaId", SqlDbType.Int).Value = route;
        command.Parameters.Add("@DispatchId", SqlDbType.UniqueIdentifier).Value = dispatch;
        command.Parameters.Add("@EventId", SqlDbType.BigInt).Value = eventId;
        command.Parameters.Add("@EventGuid", SqlDbType.UniqueIdentifier).Value = eventGuid;
        command.Parameters.Add("@OperationId", SqlDbType.UniqueIdentifier).Value = request;
        return command;
    }

    private static async Task<RouteDispatchNotificationReceipt> Read(SqlCommand command, Guid operation, Guid eventGuid, bool lookup, CancellationToken ct)
    {
        await command.Connection!.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw Invalid();
        var receipt = Checked(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), operation, eventGuid, lookup);
        await Drain(reader, ct);
        return receipt;
    }

    private static RouteDispatchNotificationReceipt Checked(Guid operation, Guid eventGuid, string state, Guid expectedOperation, Guid expectedEvent, bool lookup)
    {
        var receipt = new RouteDispatchNotificationReceipt(operation, eventGuid, state);
        bool noRecipient = operation == Guid.Empty && state == "NoRecipient";
        if (!receipt.IsValid || (!lookup || expectedOperation != Guid.Empty) && operation != expectedOperation && !noRecipient
            || expectedEvent != Guid.Empty && eventGuid != expectedEvent || lookup && state == "AlreadyRequested") throw Invalid();
        return receipt;
    }

    private static async Task Drain(SqlDataReader reader, CancellationToken ct)
    {
        while (await reader.ReadAsync(ct)) { }
        while (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) { }
    }
    private static InvalidOperationException Invalid() => new("Route dispatch notification SQL contract unavailable.");
}
