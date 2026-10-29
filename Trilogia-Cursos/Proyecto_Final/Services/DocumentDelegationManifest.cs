using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services;

public static partial class DocumentDelegationManifest
{
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
            +ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','EXECUTE'),0)+ISNULL(HAS_PERMS_BY_NAME(N'DocumentSecurity','SCHEMA','EXECUTE'),0);
            """, connection) { CommandTimeout = 5 })
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM OPENJSON(@manifest) WITH(Name nvarchar(300) '$.name',Sha varchar(64) '$.hash') x
            LEFT JOIN sys.sql_modules m ON m.object_id=OBJECT_ID(x.Name) LEFT JOIN sys.triggers t ON t.object_id=m.object_id
            WHERE ISNULL(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2),'')<>x.Sha
            OR m.uses_ansi_nulls<>1 OR m.uses_quoted_identifier<>1 OR ISNULL(t.is_disabled,0)<>0;
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
            command.Parameters.Add("@ledger", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(Ledger.Select(x => new { name = x.Key, hash = x.Value }));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        }
        using (var command = new SqlCommand("""
            SELECT ABS(2-(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DocumentSecurity')))
            +ABS(4-(SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DocumentSecurity')) AND is_disabled=0 AND is_not_trusted=0))
            +ABS(6-(SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'DocumentSecurity')) AND is_disabled=0 AND is_not_trusted=0));
            """, connection) { CommandTimeout = 5 })
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        foreach (var table in new[] { "DocumentSecurity.Grants", "DocumentSecurity.GrantAudit", "dbo.DocumentosDigitales", "dbo.DocumentoVersiones", "dbo.Stage2OriginalLogins" })
            foreach (var permission in new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "ALTER" })
                if (await Permission(table, permission) != 0) throw Invalid();
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM sys.columns c WHERE c.object_id IN(OBJECT_ID(N'DocumentSecurity.Grants'),
            OBJECT_ID(N'DocumentSecurity.GrantAudit'),OBJECT_ID(N'dbo.DocumentosDigitales'),
            OBJECT_ID(N'dbo.DocumentoVersiones'),OBJECT_ID(N'dbo.Stage2OriginalLogins'))
            AND HAS_PERMS_BY_NAME(OBJECT_SCHEMA_NAME(c.object_id)+N'.'+OBJECT_NAME(c.object_id),'OBJECT','SELECT',c.name,'COLUMN')=1;
            """, connection) { CommandTimeout = 5 })
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw Invalid();
        foreach (var operation in PublicOperations)
            if (await Permission("DocumentSecurity." + operation, "EXECUTE") != 1) throw Invalid();
        foreach (var operation in LegacyOperations)
            if (await Permission(operation, "EXECUTE") != 0) throw Invalid();
        foreach (var permission in new[] { "SELECT", "EXECUTE", "ALTER" })
            if (await Permission("DocumentSecurity.CanAccess", permission) != 0) throw Invalid();

        async Task<int> Permission(string name, string permission)
        {
            using var command = new SqlCommand("SELECT ISNULL(HAS_PERMS_BY_NAME(@name,'OBJECT',@permission),0)", connection) { CommandTimeout = 5 };
            command.Parameters.Add("@name", SqlDbType.NVarChar, 300).Value = name;
            command.Parameters.Add("@permission", SqlDbType.NVarChar, 60).Value = permission;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
    }
    private static InvalidOperationException Invalid() => new("No se acreditó el contrato documental ni su separación de privilegios.");
}
