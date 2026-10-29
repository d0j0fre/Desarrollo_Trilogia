using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services.ChatV2.Broker;

internal static partial class ChatV2PersonalManifest
{
    internal static readonly string[] Writers = ["OpenPrivate", "Prepare", "ExecuteIntent", "Leave"];
    internal static readonly string[] Readers = ["Resolve", "Directory", "Own", "Membership", "History"];
    internal static readonly string[] Tables = ["Conversations", "Intents", "Messages", "Requests", "Outbox"];
    internal static async Task VerifyAsync(string value, bool writer, string expectedMdf, string expectedLdf,
        string expectedErrorLog, string expectedServerVersion, CancellationToken ct)
    {
        string phase = "restricted-connection-configuration";
        var settings = new SqlConnectionStringBuilder(value) { Pooling = false, ConnectRetryCount = 0, ConnectTimeout = 5 };
        if (settings.IntegratedSecurity || settings.PersistSecurityInfo || string.IsNullOrWhiteSpace(settings.UserID) ||
            string.IsNullOrWhiteSpace(settings.Password) || settings.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase)) throw Invalid();
        await using var connection = new SqlConnection(settings.ConnectionString); await connection.OpenAsync(ct);
        phase = "database-login-physical-instance-identity";
        using (var command = new SqlCommand("SELECT DB_NAME(),SUSER_SNAME(),USER_NAME(),CONVERT(nvarchar(4000),SERVERPROPERTY('ErrorLogFileName')),CONVERT(nvarchar(100),SERVERPROPERTY('ProductVersion')),CONVERT(int,SERVERPROPERTY('IsLocalDB'))", connection) { CommandTimeout = 5 })
        using (var reader = await command.ExecuteReaderAsync(ct))
            if (!await reader.ReadAsync(ct) || reader.GetString(0) != settings.InitialCatalog || reader.GetString(1) != settings.UserID ||
                reader.GetString(2) != settings.UserID || reader.IsDBNull(3) || reader.GetString(3) != expectedErrorLog ||
                reader.IsDBNull(4) || reader.GetString(4) != expectedServerVersion || reader.IsDBNull(5) || reader.GetInt32(5) != 1) throw Invalid();
        phase = "physical-database-files";
        using (var command = new SqlCommand("SELECT COUNT(*) FROM sys.database_files WHERE (type=0 AND physical_name=@mdf) OR (type=1 AND physical_name=@ldf)", connection) { CommandTimeout = 5 })
        {
            command.Parameters.Add("@mdf", SqlDbType.NVarChar, 4000).Value = expectedMdf;
            command.Parameters.Add("@ldf", SqlDbType.NVarChar, 4000).Value = expectedLdf;
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 2) throw Invalid();
        }
        phase = "immutable-module-hashes";
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM OPENJSON(@manifest) WITH(Name nvarchar(300) '$.name',Sha varchar(64) '$.hash') x
            LEFT JOIN sys.sql_modules m ON m.object_id=OBJECT_ID(x.Name) LEFT JOIN sys.triggers t ON t.object_id=m.object_id
            WHERE ISNULL(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2),'')<>x.Sha
            OR m.uses_ansi_nulls<>1 OR m.uses_quoted_identifier<>1 OR ISNULL(t.is_disabled,0)<>0;
            """, connection) { CommandTimeout = 5 })
        {
            command.Parameters.Add("@manifest", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(Modules.Select(x => new { name = x.Key, hash = x.Value }));
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0) throw Invalid();
        }
        phase = "immutable-migration-ledger";
        using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0044_stage2_chat_v2_personal' AND FileSha256=@hash AND Status=N'Applied'", connection) { CommandTimeout = 5 })
        {
            command.Parameters.Add("@hash", SqlDbType.VarChar, 64).Value = MigrationHash;
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1) throw Invalid();
        }
        int stageIndex = 0;
        foreach (string sql in new[]
        {
            "SELECT COUNT(*) FROM sys.database_role_members WHERE member_principal_id=USER_ID()",
            "SELECT COUNT(*) FROM sys.fn_my_permissions(NULL,'SERVER') WHERE permission_name NOT IN(N'CONNECT SQL',N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION',N'VIEW ANY COLUMN MASTER KEY DEFINITION')",
            "SELECT ISNULL(IS_SRVROLEMEMBER('sysadmin'),0)+ISNULL(IS_SRVROLEMEMBER('securityadmin'),0)+ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),0)+ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','EXECUTE'),0)+ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','EXECUTE'),0)+ISNULL(HAS_PERMS_BY_NAME(N'ChatV2P','SCHEMA','EXECUTE'),0)",
            "SELECT ABS(5-(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'ChatV2P')))+ABS(13-(SELECT COUNT(*) FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id WHERE o.schema_id=SCHEMA_ID(N'ChatV2P')))+ABS(9-(SELECT COUNT(*) FROM sys.check_constraints WHERE schema_id=SCHEMA_ID(N'ChatV2P') AND is_disabled=0 AND is_not_trusted=0))+ABS(12-(SELECT COUNT(*) FROM sys.foreign_keys WHERE schema_id=SCHEMA_ID(N'ChatV2P') AND is_disabled=0 AND is_not_trusted=0))"
        })
        {
            phase = new[] { "database-role-membership", "server-permissions", "broad-database-permissions", "storage-structure" }[stageIndex++];
            using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0) throw Invalid();
        }
        phase = "exact-public-procedures";
        string[] expected = (writer ? Writers : Readers).Select(x => "ChatV2P." + x).ToArray();
        var actual = new HashSet<string>(StringComparer.Ordinal);
        using (var command = new SqlCommand("SELECT SCHEMA_NAME(schema_id)+N'.'+name FROM sys.procedures WHERE is_ms_shipped=0 AND HAS_PERMS_BY_NAME(SCHEMA_NAME(schema_id)+N'.'+name,'OBJECT','EXECUTE')=1", connection) { CommandTimeout = 5 })
        using (var reader = await command.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) actual.Add(reader.GetString(0));
        if (!actual.SetEquals(expected)) throw Invalid();
        phase = "private-object-data-permissions";
        string[] privateTables = Tables.Select(x => "ChatV2P." + x).Concat(["dbo.Stage2OriginalLogins", "dbo.Usuarios", "dbo.Perfiles", "dbo.Permisos", "dbo.PerfilPermisos"]).ToArray();
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM OPENJSON(@tables) x CROSS JOIN (VALUES(N'SELECT'),(N'INSERT'),(N'UPDATE'),(N'DELETE'),(N'ALTER'),(N'CONTROL')) p(Permission)
            WHERE HAS_PERMS_BY_NAME(x.value,'OBJECT',p.Permission)=1;
            """, connection) { CommandTimeout = 5 })
        {
            command.Parameters.Add("@tables", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(privateTables);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0) throw Invalid();
        }
        phase = "private-object-column-permissions";
        using (var command = new SqlCommand("""
            SELECT COUNT(*) FROM OPENJSON(@tables) x JOIN sys.columns c ON c.object_id=OBJECT_ID(x.value)
            WHERE HAS_PERMS_BY_NAME(x.value,'OBJECT','SELECT',c.name,'COLUMN')=1
               OR HAS_PERMS_BY_NAME(x.value,'OBJECT','UPDATE',c.name,'COLUMN')=1;
            """, connection) { CommandTimeout = 5 })
        {
            command.Parameters.Add("@tables", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(privateTables);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 0) throw Invalid();
        }
        InvalidOperationException Invalid() => new BrokerManifestException(phase);
    }
}
internal sealed class BrokerManifestException(string stage) : InvalidOperationException("No se acreditó el manifiesto personal y los privilegios exclusivos del broker.")
{
    internal string Stage { get; } = stage;
}
