using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public static class DeliveryNotificationManifest
{
    public const string MigrationId = "0058_stage2_delivery_notifications";
    public const string MigrationHash = "30DD0D7ACEDE56D067CA6A583C02692D071619017987A5D1E0ECF6B98FF38D63";
    public static IReadOnlyDictionary<string, string> ObjectHashes { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DeliveryNotifications.AttemptTransitions"] = "9EDA733B6928F601138488EB6FCFD879BFECE76FE1EE1BE660E915FC7E6BCB19",
        ["DeliveryNotifications.Claim"] = "A0242E34BB8828ED83D3A04E61BC5DFD5243A82C75BFF1E9A82E7A2E3CBB591D",
        ["DeliveryNotifications.Complete"] = "C9091B1DDEC8E49293F9FE577EAEEE96B6F9D8B054C18630F4AF52337CDC31B2",
        ["DeliveryNotifications.ImmutableEvents"] = "069726F0C8F016101261D9005B2CA6A13343F62C238310499D2077C3F6D0EA01",
        ["DeliveryNotifications.ImmutableRequests"] = "06CDEF1A1A83BA2F5C170DF5CC16FA7960A8EE0D1CD20D23852D378CE8003282",
        ["DeliveryNotifications.Receipt"] = "8B15B325E2CD1AF0D7D3FB981523B288F7EA3F2E9BC8CF49C9FE6FD702546BCC",
        ["DeliveryNotifications.Request"] = "442E88FE485E94895C807F8198F98A3374EEF8E420A63A6FFD455072344C5C11",
        ["DeliveryNotifications.RequireAccess"] = "6E62F75B4F0A9D7E227B5EE0BCD7E87F3BAF28614742401F5C24B58A478B05CF",
        ["DeliveryNotifications.SnapshotHash"] = "077D8EB802BEEF622B1C8ABD16302C9DF0552C57EEEB4017CE4092B54B1F2A73",
        ["dbo.sp_Stage2_RequireOriginalLogin"] = "8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C",
        ["dbo.sp_Stage2_ConfirmDelivery"] = "7E8763B2CA8D87462FE2A1D109C07BCD28175E779C1F39E5E752DC5673227866"
    };
    public static IReadOnlyList<string> PublicOperations { get; } = ["Request", "Claim", "Complete", "Receipt"];

    // RestrictedSqlGate has a closed list of Chat roles. This authority deliberately has its own smaller contract.
    public static async Task VerifyAsync(IConfiguration configuration)
    {
        if (MigrationHash.Length != 64 || ObjectHashes.Any(x => x.Value.Length != 64)) throw Invalid();
        var settings = DeliveryNotificationConfiguration.Connection(configuration); settings.Pooling = false;
        await using var connection = new SqlConnection(settings.ConnectionString);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.OpenAsync(deadline.Token);
        if (await Scalar("SELECT CASE WHEN USER_NAME()=@user AND ORIGINAL_LOGIN()=@user AND DB_NAME()=@db THEN 0 ELSE 1 END;",
            ("@user", settings.UserID), ("@db", settings.InitialCatalog)) != 0) throw Invalid();
        if (await Scalar("""
            SELECT ISNULL(IS_SRVROLEMEMBER('sysadmin'),0)+ISNULL(IS_SRVROLEMEMBER('securityadmin'),0)
             +ISNULL(IS_SRVROLEMEMBER('serveradmin'),0)+ISNULL(IS_SRVROLEMEMBER('setupadmin'),0)
             +ISNULL(IS_SRVROLEMEMBER('processadmin'),0)+ISNULL(IS_SRVROLEMEMBER('bulkadmin'),0)
             +ISNULL(IS_SRVROLEMEMBER('diskadmin'),0)+ISNULL(IS_SRVROLEMEMBER('dbcreator'),0)
             +(SELECT COUNT(*) FROM sys.user_token WHERE type=N'DATABASE ROLE' AND name<>N'public')
             +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),0)
             +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','EXECUTE'),0)
             +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'),0)
             +ISNULL(HAS_PERMS_BY_NAME(N'DeliveryNotifications','SCHEMA','EXECUTE'),0)
             +ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','EXECUTE'),0);
            """) != 0) throw Invalid();
        if (await Scalar("SELECT ABS(1-COUNT(*)) FROM dbo.SchemaMigrationHistory WHERE MigrationId=@id AND FileSha256=@hash AND Status=N'Applied';",
            ("@id", MigrationId), ("@hash", MigrationHash)) != 0) throw Invalid();
        var json = JsonSerializer.Serialize(ObjectHashes.Select(x => new { name = x.Key, hash = x.Value }));
        if (await Scalar("""
            SELECT COUNT(*) FROM OPENJSON(@manifest) WITH(Name nvarchar(300) '$.name', Sha varchar(64) '$.hash') x
            LEFT JOIN sys.sql_modules m ON m.object_id=OBJECT_ID(x.Name)
            LEFT JOIN sys.triggers t ON t.object_id=m.object_id
            WHERE ISNULL(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2),'')<>x.Sha
             OR ISNULL(t.is_disabled,0)<>0;
            """, ("@manifest", json)) != 0) throw Invalid();
        if (await Scalar("""
            SELECT ABS(3-(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications')))
             +ABS(16-(SELECT COUNT(*) FROM sys.columns WHERE object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications'))))
             +ABS(2-(SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications')) AND is_disabled=0 AND is_not_trusted=0))
             +ABS(6-(SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DeliveryNotifications')) AND is_disabled=0 AND is_not_trusted=0))
             +ABS(1-(SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'DeliveryNotifications.Requests') AND name=N'UX_DeliveryNotification_Event' AND is_unique=1 AND has_filter=0 AND is_disabled=0));
            """) != 0) throw Invalid();
        foreach (var operation in PublicOperations)
            if (await Permission("DeliveryNotifications." + operation, "EXECUTE") != 1) throw Invalid();
        if (await Scalar("""
            SELECT COUNT(*) FROM sys.procedures p
            WHERE HAS_PERMS_BY_NAME(QUOTENAME(OBJECT_SCHEMA_NAME(p.object_id))+N'.'+QUOTENAME(p.name),'OBJECT','EXECUTE')=1
             AND NOT(OBJECT_SCHEMA_NAME(p.object_id)=N'DeliveryNotifications' AND p.name IN(N'Request',N'Claim',N'Complete',N'Receipt'));
            """) != 0) throw Invalid();
        foreach (var operation in new[] { "DeliveryNotifications.RequireAccess", "DeliveryNotifications.SnapshotHash", "dbo.sp_Stage2_RequireOriginalLogin",
            "dbo.sp_Stage2_CreateOriginalLogin", "dbo.sp_Auth_GetLoginCredential" })
            if (await Permission(operation, "EXECUTE") != 0) throw Invalid();
        foreach (var table in new[] { "DeliveryNotifications.Requests", "DeliveryNotifications.Attempts", "DeliveryNotifications.Events", "dbo.RutaPedidos", "dbo.Rutas", "dbo.EntregaSyncLog", "dbo.Pedidos", "dbo.Usuarios", "dbo.Stage2OriginalLogins" })
        {
            // fn_my_permissions includes column grants that HAS_PERMS_BY_NAME(table,'OBJECT','SELECT') can miss.
            if (await Scalar("SELECT COUNT(*) FROM sys.fn_my_permissions(@obj,'OBJECT') WHERE permission_name IN(N'SELECT',N'INSERT',N'UPDATE',N'DELETE',N'ALTER',N'CONTROL',N'TAKE OWNERSHIP');", ("@obj", table)) != 0) throw Invalid();
        }
        if (await Scalar("""
            SELECT COUNT(*) FROM sys.tables t
            CROSS APPLY sys.fn_my_permissions(QUOTENAME(OBJECT_SCHEMA_NAME(t.object_id))+N'.'+QUOTENAME(t.name),'OBJECT') p
            WHERE t.is_ms_shipped=0 AND p.permission_name IN(N'SELECT',N'INSERT',N'UPDATE',N'DELETE',N'ALTER',N'CONTROL',N'TAKE OWNERSHIP')
             AND NOT(OBJECT_SCHEMA_NAME(t.object_id)=N'dbo' AND t.name=N'SchemaMigrationHistory' AND p.permission_name=N'SELECT'
                AND p.subentity_name IN(N'MigrationId',N'FileName',N'FileSha256',N'Status'));
            """) != 0) throw Invalid();
        async Task<int> Permission(string name, string permission) => await Scalar("SELECT ISNULL(HAS_PERMS_BY_NAME(@obj,'OBJECT',@permission),0);", ("@obj", name), ("@permission", permission));
        async Task<int> Scalar(string sql, params (string Name, string Value)[] values)
        {
            using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
            foreach (var value in values) command.Parameters.Add(value.Name, SqlDbType.NVarChar, -1).Value = value.Value;
            return Convert.ToInt32(await command.ExecuteScalarAsync(deadline.Token));
        }
    }
    private static InvalidOperationException Invalid() => new("Delivery notification SQL composition unavailable.");
}
