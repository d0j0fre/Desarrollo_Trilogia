using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public static class RouteDispatchNotificationManifest
{
    public const string MigrationId = "0059_stage2_route_dispatch_notifications";
    public const string MigrationHash = "9CF9E0526E0142AB1B6A268ACF23C6DEF1B0B46781FAC03BC4338F9D7D2BCD21";
    public static IReadOnlyDictionary<string, string> ObjectHashes { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dbo.sp_Stage2_RouteOperation"] = "01D6926AB658F22D1EB30E88A5B17D57691C000E5DC8297BD3A47AE48906C3A2",
        ["RouteDispatch.AttemptTransitions"] = "660F3800D53FFEB082141BDBDBA87DDDA22EFAD7E8BFEC9C550FFFFC1A6B492F",
        ["RouteDispatch.Claim"] = "B0B53FD7ADBC9C596F94661FEF7C3F42AF4DC429B37CDBD38A0FA2DB59A273B7",
        ["RouteDispatch.Complete"] = "EF256B149EF85C3305B2CBB1D0BB25118BB9695AB55DE70E466FAABCE6D77EA3",
        ["RouteDispatch.Dispatch"] = "0B918DB729C4E1B05F83D39F15AE522CD54C4803B67871F7D5BFA93D1E6728AD",
        ["RouteDispatch.GetOwned"] = "36719ECCF8B2B46DF73498705D1DD0C289D17E1FC32E63B701F7C0D511156BDA",
        ["RouteDispatch.ImmutableDispatches"] = "02E7ED4EF66454CC246CE52FE8B23D19023E6ACC6E158E11FD97A3936A082B83",
        ["RouteDispatch.ImmutableEvents"] = "A9B2FAF95B5B46840366A341859A9C95E68C2D1438660D79ED804AB2E13F7006",
        ["RouteDispatch.ImmutableItems"] = "1460F685BC632CF279D030E09ACEC3F0D6BE23F4A8D1D8AEAA3F38569E556B14",
        ["RouteDispatch.ImmutableRequests"] = "70AB921C85C7FEAA781F10BC88BF0F2637763E2E7A4848A35BFDCD53DC72EEC4",
        ["RouteDispatch.Receipt"] = "9E5260E81AAEB6EF1DFC2A716A31C27513EF5A952A09E4FD9079CA2DAFC555F1",
        ["RouteDispatch.Request"] = "80A741C036A0D53FBD4DC1ADCB7432CC16718C540509B6A59EF2361E3BA63B0F",
        ["RouteDispatch.SnapshotHash"] = "75A7849AAB1E1BA821AB3F8E17240B3F0731CDF3AE47F76711CCCB50EA46AA00"
    };
    public static IReadOnlyList<string> PublicOperations { get; } = ["Request", "Claim", "Complete", "Receipt"];

    public static async Task VerifyAsync(IConfiguration configuration)
    {
        var settings = RouteDispatchNotificationConfiguration.Connection(configuration);
        if (MigrationHash.Length != 64 || ObjectHashes.Count == 0 || ObjectHashes.Any(x => x.Value.Length != 64)) throw Invalid();
        settings.Pooling = false;
        await using var connection = new SqlConnection(settings.ConnectionString);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.OpenAsync(deadline.Token);
        if (await Scalar("SELECT CASE WHEN USER_NAME()=@user AND ORIGINAL_LOGIN()=@user AND DB_NAME()=@db THEN 0 ELSE 1 END;", ("@user", settings.UserID), ("@db", settings.InitialCatalog)) != 0) throw Invalid();
        if (await Scalar("""
SELECT ISNULL(IS_SRVROLEMEMBER('sysadmin'),0)+ISNULL(IS_SRVROLEMEMBER('securityadmin'),0)
 +ISNULL(IS_SRVROLEMEMBER('serveradmin'),0)+ISNULL(IS_SRVROLEMEMBER('setupadmin'),0)
 +ISNULL(IS_SRVROLEMEMBER('processadmin'),0)+ISNULL(IS_SRVROLEMEMBER('bulkadmin'),0)
 +ISNULL(IS_SRVROLEMEMBER('diskadmin'),0)+ISNULL(IS_SRVROLEMEMBER('dbcreator'),0)
 +ISNULL(HAS_PERMS_BY_NAME(NULL,'SERVER','CONTROL SERVER'),0)
 +ISNULL(HAS_PERMS_BY_NAME(NULL,'SERVER','ALTER ANY LOGIN'),0)
 +ISNULL(HAS_PERMS_BY_NAME(NULL,'SERVER','IMPERSONATE ANY LOGIN'),0)
 +(SELECT COUNT(*) FROM sys.user_token WHERE type=N'DATABASE ROLE' AND name<>N'public')
 +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),0)
 +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','EXECUTE'),0)
 +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'),0)
 +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','TAKE OWNERSHIP'),0)
 +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER ANY USER'),0)
 +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER ANY ROLE'),0)
 +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','IMPERSONATE ANY USER'),0)
 +ISNULL(HAS_PERMS_BY_NAME(N'RouteDispatch','SCHEMA','EXECUTE'),0)
 +ISNULL(HAS_PERMS_BY_NAME(N'RouteDispatch','SCHEMA','ALTER'),0)
 +ISNULL(HAS_PERMS_BY_NAME(N'RouteDispatch','SCHEMA','CONTROL'),0)
 +ISNULL(HAS_PERMS_BY_NAME(N'RouteDispatch','SCHEMA','TAKE OWNERSHIP'),0)
 +ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','EXECUTE'),0)
 +ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','ALTER'),0)
 +ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','CONTROL'),0)
 +ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','TAKE OWNERSHIP'),0);
""") != 0) throw Invalid();
        if (await Scalar("SELECT ABS(1-COUNT(*)) FROM dbo.SchemaMigrationHistory WHERE MigrationId=@id AND FileSha256=@hash AND Status=N'Applied';", ("@id", MigrationId), ("@hash", MigrationHash)) != 0) throw Invalid();
        var json = JsonSerializer.Serialize(ObjectHashes.Select(x => new { name = x.Key, hash = x.Value }));
        if (await Scalar("""
SELECT COUNT(*) FROM OPENJSON(@manifest) WITH(Name nvarchar(300) '$.name',Sha varchar(64) '$.hash') x
LEFT JOIN sys.sql_modules m ON m.object_id=OBJECT_ID(x.Name)
LEFT JOIN sys.triggers t ON t.object_id=m.object_id
WHERE ISNULL(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2),'')<>x.Sha OR ISNULL(t.is_disabled,0)<>0;
""", ("@manifest", json)) != 0) throw Invalid();
        foreach (var operation in PublicOperations)
            if (await Permission("RouteDispatch." + operation, "EXECUTE") != 1) throw Invalid();
        if (await Scalar("""
SELECT COUNT(*) FROM sys.procedures p
WHERE HAS_PERMS_BY_NAME(QUOTENAME(OBJECT_SCHEMA_NAME(p.object_id))+N'.'+QUOTENAME(p.name),'OBJECT','EXECUTE')=1
 AND NOT(OBJECT_SCHEMA_NAME(p.object_id)=N'RouteDispatch' AND p.name IN(N'Request',N'Claim',N'Complete',N'Receipt'));
""") != 0) throw Invalid();
        foreach (var operation in new[] { "RouteDispatch.Dispatch", "RouteDispatch.GetOwned", "RouteDispatch.RequireAccess", "dbo.sp_Stage2_RequireOriginalLogin", "dbo.sp_Stage2_CreateOriginalLogin", "dbo.sp_Auth_GetLoginCredential", "dbo.sp_Rutas_Dispatch" })
            if (await Permission(operation, "EXECUTE") != 0) throw Invalid();
        if (await Scalar("""
SELECT COUNT(*) FROM sys.objects o
CROSS APPLY sys.fn_my_permissions(QUOTENAME(OBJECT_SCHEMA_NAME(o.object_id))+N'.'+QUOTENAME(o.name),'OBJECT') p
WHERE o.is_ms_shipped=0 AND o.type IN(N'P',N'PC',N'TR')
 AND p.permission_name IN(N'ALTER',N'CONTROL',N'TAKE OWNERSHIP');
""") != 0) throw Invalid();
        if (await Scalar("""
SELECT COUNT(*) FROM sys.database_principals d
CROSS APPLY sys.fn_my_permissions(QUOTENAME(d.name),'USER') p
WHERE d.principal_id>4 AND d.name<>USER_NAME() AND p.permission_name=N'IMPERSONATE';
""") != 0) throw Invalid();
        if (await Scalar("""
SELECT COUNT(*) FROM sys.server_principals s
CROSS APPLY sys.fn_my_permissions(QUOTENAME(s.name),'LOGIN') p
WHERE s.name<>ORIGINAL_LOGIN() AND p.permission_name=N'IMPERSONATE';
""") != 0) throw Invalid();
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
    private static InvalidOperationException Invalid() => new("Route dispatch notification SQL composition unavailable.");
}
