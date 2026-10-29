using System.Data;
using JJ.Commerce;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public sealed class DeliveryNotificationStore(IConfiguration configuration):IDeliveryNotificationStore
{
    public async Task<DeliveryNotificationReceipt> RequestAsync(DeliveryNotificationCommand value,CancellationToken ct)
    {
        await using var connection=Connection();
        await using var command=SessionCommand(connection,"Request",value.Session,value.RouteOrderId,value.SyncGuid,value.OperationId);
        return await ReadReceipt(command,value.OperationId,value.SyncGuid,false,ct);
    }
    public async Task<DeliveryNotificationClaim> ClaimAsync(DeliveryNotificationCommand value,byte[] token,CancellationToken ct)
    {
        await using var connection=Connection();
        await using var command=SessionCommand(connection,"Claim",value.Session,value.RouteOrderId,value.SyncGuid,value.OperationId);
        command.Parameters.Add("@ClaimToken",SqlDbType.Binary,32).Value=token;
        await connection.OpenAsync(ct);await using var reader=await command.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))throw Invalid();
        var receipt=Checked(reader.GetGuid(0),reader.GetGuid(1),reader.GetString(3),value.OperationId,value.SyncGuid,false);
        bool send=reader.GetBoolean(2);
        var snapshot=send?new DeliveryNotificationSnapshot(reader.GetInt32(4),reader.GetString(5),
            DateTime.SpecifyKind(reader.GetDateTime(6),DateTimeKind.Unspecified),reader.GetString(7),reader.GetString(8)):null;
        // Observe all resultsets, command completion and possible late SQL errors before SMTP can run.
        await Drain(reader,ct);return new(receipt,send,snapshot);
    }
    public async Task<DeliveryNotificationReceipt> CompleteAsync(Guid operation,byte[] token,string state,CancellationToken ct)
    {
        await using var connection=Connection();
        await using var command=new SqlCommand("DeliveryNotifications.Complete",connection){CommandType=CommandType.StoredProcedure,CommandTimeout=5};
        command.Parameters.Add("@OperationId",SqlDbType.UniqueIdentifier).Value=operation;
        command.Parameters.Add("@ClaimToken",SqlDbType.Binary,32).Value=token;
        command.Parameters.Add("@State",SqlDbType.NVarChar,20).Value=state;
        return await ReadReceipt(command,operation,Guid.Empty,false,ct);
    }
    public async Task<DeliveryNotificationReceipt> ReceiptAsync(DeliveryNotificationReceiptCommand value,CancellationToken ct)
    {
        await using var connection=Connection();
        await using var command=SessionCommand(connection,"Receipt",value.Session,value.RouteOrderId,value.SyncGuid,value.OperationId);
        return await ReadReceipt(command,value.OperationId,value.SyncGuid,true,ct);
    }
    private SqlConnection Connection()=>new(DeliveryNotificationConfiguration.Connection(configuration).ConnectionString);
    internal static SqlCommand SessionCommand(SqlConnection connection,string operation,CommerceSession session,int routeOrder,Guid sync,Guid request)
    {
        if(operation is not("Request" or "Claim" or "Receipt"))throw Invalid();
        var command=new SqlCommand("DeliveryNotifications."+operation,connection){CommandType=CommandType.StoredProcedure,CommandTimeout=10};
        command.Parameters.Add("@LoginId",SqlDbType.UniqueIdentifier).Value=session.LoginId;
        command.Parameters.Add("@UsuarioId",SqlDbType.Int).Value=session.UserId;
        command.Parameters.Add("@PerfilId",SqlDbType.Int).Value=session.ProfileId;
        command.Parameters.Add("@SecurityStamp",SqlDbType.UniqueIdentifier).Value=session.SecurityStamp;
        command.Parameters.Add("@BindingHash",SqlDbType.Binary,32).Value=session.BindingHash;
        command.Parameters.Add(new SqlParameter("@IssuedUtc",SqlDbType.DateTime2){Scale=7,Value=session.IssuedUtc});
        command.Parameters.Add(new SqlParameter("@ExpiresUtc",SqlDbType.DateTime2){Scale=7,Value=session.ExpiresUtc});
        command.Parameters.Add("@RouteOrderId",SqlDbType.Int).Value=routeOrder;
        command.Parameters.Add("@SyncGuid",SqlDbType.UniqueIdentifier).Value=sync;
        command.Parameters.Add("@OperationId",SqlDbType.UniqueIdentifier).Value=request;
        return command;
    }
    private static async Task<DeliveryNotificationReceipt> ReadReceipt(SqlCommand command,Guid operation,Guid sync,bool lookup,CancellationToken ct)
    {
        await command.Connection!.OpenAsync(ct);await using var reader=await command.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))throw Invalid();
        var receipt=Checked(reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2),operation,sync,lookup);
        await Drain(reader,ct);return receipt;
    }
    private static DeliveryNotificationReceipt Checked(Guid operation,Guid sync,string state,Guid expectedOperation,Guid expectedSync,bool lookup)
    {
        var receipt=new DeliveryNotificationReceipt(operation,sync,state);
        if(!receipt.IsValid||(!lookup||expectedOperation!=Guid.Empty)&&operation!=expectedOperation
            ||expectedSync!=Guid.Empty&&sync!=expectedSync||lookup&&state=="AlreadyRequested")throw Invalid();
        return receipt;
    }
    private static async Task Drain(SqlDataReader reader,CancellationToken ct)
    {while(await reader.ReadAsync(ct)){}while(await reader.NextResultAsync(ct))while(await reader.ReadAsync(ct)){}}
    private static InvalidOperationException Invalid()=>new("Delivery notification SQL contract unavailable.");
}
