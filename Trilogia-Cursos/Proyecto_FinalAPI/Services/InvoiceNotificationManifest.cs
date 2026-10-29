using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public static class InvoiceNotificationManifest
{
    public const string MigrationId = "0057_stage2_invoice_notifications";
    public const string MigrationHash = "232F3736B6B4DB289BC12A049BD2D6ABB26F82E7F85A5DDF2003EC379A70DAF1";
    public static IReadOnlyDictionary<string, string> ObjectHashes { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["InvoiceNotifications.AttemptTransitions"] = "E41F00912069D761A5E3436B27650FF2458354838065C2B5ED6598DD84B36DA5",
        ["InvoiceNotifications.Claim"] = "25A6EA7CEDB67697221E740E1EC1A93E38983B7AD945522AB67E7A8524323BF5",
        ["InvoiceNotifications.Complete"] = "3B7FFA78FC25DE2F3203F83982407A267D5564CDD888D23D712022F960562C24",
        ["InvoiceNotifications.ImmutableEvents"] = "0BE8966905CAE78720CCC62013D1F1C97FEB8EADCD39A7836E157C7D76B0E8DA",
        ["InvoiceNotifications.ImmutableRequests"] = "33E108133E605C4226D96ED9822D42C509816B13B625A1811DE5F849F3B0CC91",
        ["InvoiceNotifications.Receipt"] = "C646ED74D648BC1BB56E3DC23FE121F763416F5C179DBAAE557C264BE35A3558",
        ["InvoiceNotifications.Request"] = "8561421F9B791AF6D05B4C61BCDAFFD6C2364D311AA003C1615D85389F65CDF9",
        ["InvoiceNotifications.RequireAccess"] = "5430DBD2FAF9F1394C5EC427BC52841D0566DDE9FEABE0EB95186B48D2D0C5B3",
        ["InvoiceNotifications.SnapshotHash"] = "0CF1E06208E7307BA6C8EF212218CB1431EAA8248B88B83150FAD262B0F1A774",
        ["dbo.sp_Stage2_RequireOriginalLogin"] = "8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C"
    };
    public static IReadOnlyList<string> PublicOperations { get; } = ["Request", "Claim", "Complete", "Receipt"];

    // RestrictedSqlGate has a closed list of Chat roles. This authority deliberately has its own smaller contract.
    public static async Task VerifyAsync(IConfiguration configuration)
    {
        var settings = InvoiceNotificationConfiguration.Connection(configuration); settings.Pooling = false;
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
             +ISNULL(HAS_PERMS_BY_NAME(N'InvoiceNotifications','SCHEMA','EXECUTE'),0)
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
            SELECT ABS(3-(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications')))
             +ABS(16-(SELECT COUNT(*) FROM sys.columns WHERE object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications'))))
             +ABS(2-(SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications')) AND is_disabled=0 AND is_not_trusted=0))
             +ABS(5-(SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'InvoiceNotifications')) AND is_disabled=0 AND is_not_trusted=0))
             +ABS(1-(SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'InvoiceNotifications.Requests') AND name=N'UX_InvoiceNotification_Initial' AND is_unique=1 AND has_filter=1 AND is_disabled=0));
            """) != 0) throw Invalid();
        foreach (var operation in PublicOperations)
            if (await Permission("InvoiceNotifications." + operation, "EXECUTE") != 1) throw Invalid();
        if (await Scalar("""
            SELECT COUNT(*) FROM sys.procedures p
            WHERE HAS_PERMS_BY_NAME(QUOTENAME(OBJECT_SCHEMA_NAME(p.object_id))+N'.'+QUOTENAME(p.name),'OBJECT','EXECUTE')=1
             AND NOT(OBJECT_SCHEMA_NAME(p.object_id)=N'InvoiceNotifications' AND p.name IN(N'Request',N'Claim',N'Complete',N'Receipt'));
            """) != 0) throw Invalid();
        foreach (var operation in new[] { "InvoiceNotifications.RequireAccess", "InvoiceNotifications.SnapshotHash", "dbo.sp_Stage2_RequireOriginalLogin",
            "dbo.sp_Stage2_CreateOriginalLogin", "dbo.sp_Auth_GetLoginCredential" })
            if (await Permission(operation, "EXECUTE") != 0) throw Invalid();
        foreach (var table in new[] { "InvoiceNotifications.Requests", "InvoiceNotifications.Attempts", "InvoiceNotifications.Events", "dbo.Facturas", "dbo.Usuarios", "dbo.Stage2OriginalLogins" })
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
    private static InvalidOperationException Invalid() => new("Invoice notification SQL composition unavailable.");
}
