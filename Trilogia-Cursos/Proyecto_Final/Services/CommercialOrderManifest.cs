using System.Data;
using System.Text.Json;
using JJ.Commerce;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services;

public static class CommercialOrderManifest
{
    public static IReadOnlyDictionary<string, string> Modules { get; } = OrderSqlContract.ObjectHashes
        .Concat(QuoteSqlContract.ObjectHashes).ToDictionary(x => x.Key, x => x.Value);
    public static IReadOnlyList<string> MetadataObjects { get; } = QuoteSqlContract.MetadataObjects
        .Concat(OrderSqlContract.ObjectHashes.Keys.Where(x => x.StartsWith("CommercialOrders.", StringComparison.Ordinal) && !x.EndsWith("Immutable", StringComparison.Ordinal)))
        .Concat(OrderSqlContract.Tables.Select(x => "CommercialOrders." + x))
        .Concat(new[] { "dbo.Stage2OrderAmounts", "dbo.Stage2OrderAmountLines" }).Distinct().ToArray();

    public static async Task VerifyAsync(string connectionString)
    {
        var settings = new SqlConnectionStringBuilder(connectionString) { Pooling = false, ConnectRetryCount = 0, ConnectTimeout = 5 };
        if (settings.IntegratedSecurity || settings.PersistSecurityInfo || string.IsNullOrWhiteSpace(settings.UserID)
            || string.IsNullOrWhiteSpace(settings.Password) || settings.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase)) throw Invalid();
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        using (var command = new SqlCommand("""
            SELECT ISNULL(IS_SRVROLEMEMBER('sysadmin'),0)+ISNULL(IS_SRVROLEMEMBER('securityadmin'),0)
            +ISNULL(IS_ROLEMEMBER('db_owner'),0)+ISNULL(IS_ROLEMEMBER('db_securityadmin'),0)+ISNULL(IS_ROLEMEMBER('db_ddladmin'),0)
            +ISNULL(IS_ROLEMEMBER('db_datareader'),0)+ISNULL(IS_ROLEMEMBER('db_datawriter'),0)
            +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),0)+ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','EXECUTE'),0)
            +ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','EXECUTE'),0)+ISNULL(HAS_PERMS_BY_NAME(N'CommercialOrders','SCHEMA','EXECUTE'),0)
            +ISNULL(HAS_PERMS_BY_NAME(N'CommerceQuotes','SCHEMA','EXECUTE'),0);
            """, connection) { CommandTimeout = 5 })
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM OPENJSON(@manifest) WITH(Name nvarchar(300) '$.name',Sha varchar(64) '$.hash') x
            LEFT JOIN sys.sql_modules m ON m.object_id=OBJECT_ID(x.Name) LEFT JOIN sys.triggers t ON t.object_id=m.object_id
            WHERE ISNULL(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2),'')<>x.Sha OR ISNULL(t.is_disabled,0)<>0;
            """, connection) { CommandTimeout = 5 })
        {
            command.Parameters.Add("@manifest", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(Modules.Select(x => new { name = x.Key, hash = x.Value }));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        }
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM OPENJSON(@ledger) WITH(Name nvarchar(300) '$.name',Sha varchar(64) '$.hash') x
            LEFT JOIN dbo.SchemaMigrationHistory h ON h.MigrationId=x.Name AND h.FileSha256=x.Sha AND h.Status=N'Applied'
            WHERE h.MigrationId IS NULL;
            """, connection) { CommandTimeout = 5 })
        {
            command.Parameters.Add("@ledger", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(OrderSqlContract.Ledger.Select(x => new { name = x.Key, hash = x.Value }));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        }
        using (var command = new SqlCommand("""
            SELECT ABS(3-(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommercialOrders')))
            +ABS(3-(SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommercialOrders')) AND is_disabled=0 AND is_not_trusted=0))
            +ABS(9-(SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommercialOrders')) AND is_disabled=0 AND is_not_trusted=0));
            """, connection) { CommandTimeout = 5 })
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        foreach (var table in OrderSqlContract.Tables.Select(x => "CommercialOrders." + x)
            .Concat(QuoteSqlContract.Tables.Select(x => "CommerceQuotes." + x)).Append("dbo.Stage2OriginalLogins"))
            foreach (var permission in new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "ALTER" })
                if (await Permission(table, permission) != 0) throw Invalid();
        foreach (var operation in OrderSqlContract.PublicOperations)
            if (await Permission("CommercialOrders." + operation, "EXECUTE") != 1) throw Invalid();
        foreach (var operation in OrderSqlContract.InternalOperations)
            if (await Permission("CommercialOrders." + operation, "EXECUTE") != 0) throw Invalid();
        foreach (var operation in new[] { "Capture", "Publish", "RequireOwner", "CaptureSource" })
            if (await Permission("CommerceQuotes." + operation, "EXECUTE") != 0) throw Invalid();

        async Task<int> Permission(string name, string permission)
        {
            using var command = new SqlCommand("SELECT ISNULL(HAS_PERMS_BY_NAME(@name,'OBJECT',@permission),0)", connection) { CommandTimeout = 5 };
            command.Parameters.Add("@name", SqlDbType.NVarChar, 300).Value = name;
            command.Parameters.Add("@permission", SqlDbType.NVarChar, 60).Value = permission;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
    }
    private static InvalidOperationException Invalid() => new("No se acreditó el contrato SQL comercial o sus privilegios.");
}
