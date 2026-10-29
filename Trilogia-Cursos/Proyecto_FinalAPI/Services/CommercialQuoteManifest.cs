using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public static class CommercialQuoteManifest
{
    public static IReadOnlyDictionary<string, string> ObjectHashes => JJ.Commerce.QuoteSqlContract.ObjectHashes;
    public static IReadOnlyList<string> Tables => JJ.Commerce.QuoteSqlContract.Tables;
    public static IReadOnlyList<string> MetadataObjects => JJ.Commerce.QuoteSqlContract.MetadataObjects;

    public static async Task VerifyAsync(string connectionString)
    {
        var settings = new SqlConnectionStringBuilder(connectionString) { Pooling = false, ConnectRetryCount = 0, ConnectTimeout = 5 };
        if (settings.IntegratedSecurity || string.IsNullOrWhiteSpace(settings.UserID) || string.IsNullOrWhiteSpace(settings.Password)) throw Invalid();
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await RequireZero("SELECT ISNULL(IS_SRVROLEMEMBER('sysadmin'),0)+ISNULL(IS_SRVROLEMEMBER('securityadmin'),0)+ISNULL(IS_ROLEMEMBER('db_owner'),0)+ISNULL(IS_ROLEMEMBER('db_securityadmin'),0)+ISNULL(IS_ROLEMEMBER('db_ddladmin'),0)+ISNULL(IS_ROLEMEMBER('db_datareader'),0)+ISNULL(IS_ROLEMEMBER('db_datawriter'),0)+ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),0)+ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','EXECUTE'),0)");
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM OPENJSON(@manifest) WITH(Name nvarchar(300) '$.name', Sha varchar(64) '$.hash') x
            LEFT JOIN sys.sql_modules m ON m.object_id=OBJECT_ID(x.Name)
            LEFT JOIN sys.triggers t ON t.object_id=m.object_id
            WHERE ISNULL(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2),'')<>x.Sha
               OR ISNULL(t.is_disabled,0)<>0;
            """, connection) { CommandTimeout = 5 })
        {
            command.Parameters.Add("@manifest", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(ObjectHashes.Select(x => new { name = x.Key, hash = x.Value }));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        }
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM dbo.SchemaMigrationHistory WHERE Status=N'Applied' AND
              ((MigrationId=N'0049_stage2_server_quote_authority' AND FileSha256=N'F8E08FCEAB1E8ED72DFDC30CF8F02DACEBD4303E3ECF292B72FB74F0520CEF7E')
               OR (MigrationId=N'0050_stage2_quote_empty_promotions' AND FileSha256=N'46616159907485A026785648A690727FECCF50CB0EAB17343A6D692406565BE5'));
            """, connection) { CommandTimeout = 5 })
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 2) throw Invalid();
        await RequireZero("SELECT ABS(4-(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommerceQuotes')))+ABS(8-(SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommerceQuotes')) AND is_disabled=0 AND is_not_trusted=0))+ABS(7-(SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'CommerceQuotes')) AND is_disabled=0 AND is_not_trusted=0))");
        foreach (var table in Tables.Select(x => "CommerceQuotes." + x).Concat(new[] { "dbo.Productos", "dbo.Promociones", "dbo.Usuarios" }))
            foreach (var permission in new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "ALTER" })
                if (await Permission(table, permission) != 0) throw Invalid();
        foreach (var operation in new[] { "Capture", "Publish", "ReadOwn" })
            if (await Permission("CommerceQuotes." + operation, "EXECUTE") != 1) throw Invalid();
        foreach (var operation in new[] { "CaptureSource", "RequireOwner" })
            if (await Permission("CommerceQuotes." + operation, "EXECUTE") != 0) throw Invalid();

        async Task RequireZero(string sql)
        {
            using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        }
        async Task<int> Permission(string name, string permission)
        {
            using var command = new SqlCommand("SELECT ISNULL(HAS_PERMS_BY_NAME(@name,'OBJECT',@permission),0)", connection) { CommandTimeout = 5 };
            command.Parameters.Add("@name", SqlDbType.NVarChar, 300).Value = name;
            command.Parameters.Add("@permission", SqlDbType.NVarChar, 60).Value = permission;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
    }
    private static InvalidOperationException Invalid() => new("El contrato SQL de precios o sus privilegios no están acreditados.");
}
