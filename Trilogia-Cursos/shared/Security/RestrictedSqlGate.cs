using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace DistribuidoraJJ.Security;

// Linked into each host. No credentials, public endpoints or broad metadata grants.
internal static class RestrictedSqlGate
{
    internal static async Task VerifyAsync(IConfiguration configuration, params (string Connection, string Role)[] roles)
    {
        foreach (var (name, role) in roles)
        {
            if (role is not ("App" or "Reader" or "Auth" or "Issuer" or "MvcApp" or "ArchiveReader" or "BrokerApp" or "BrokerReader" or "Pricing")) throw Failure();
            var settings = new SqlConnectionStringBuilder(configuration.GetConnectionString(name));
            if (settings.IntegratedSecurity) throw Failure();
            settings.ConnectTimeout = 5; settings.ConnectRetryCount = 0; settings.Pooling = false;
            try
            {
                await using var connection = new SqlConnection(settings.ConnectionString);
                await connection.OpenAsync();
                await using var broad = new SqlCommand("""
                    SELECT ISNULL(IS_SRVROLEMEMBER('sysadmin'),0)+ISNULL(IS_SRVROLEMEMBER('securityadmin'),0)
                      +ISNULL(IS_ROLEMEMBER('db_owner'),0)+ISNULL(IS_ROLEMEMBER('db_securityadmin'),0)
                      +ISNULL(IS_ROLEMEMBER('db_ddladmin'),0)+ISNULL(IS_ROLEMEMBER('db_datareader'),0)
                      +ISNULL(IS_ROLEMEMBER('db_datawriter'),0)
                      +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),0)
                      +ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','EXECUTE'),0)
                      +ISNULL(HAS_PERMS_BY_NAME(N'dbo','SCHEMA','EXECUTE'),0)
                      +ISNULL(HAS_PERMS_BY_NAME(N'ChatV2P','SCHEMA','EXECUTE'),0);
                    """, connection) { CommandTimeout = 5 };
                if (Convert.ToInt32(await broad.ExecuteScalarAsync()) != 0) throw Failure();
                foreach (var table in new[] { "ChatV2P.Intents", "ChatV2P.Messages", "ChatV2P.Requests", "ChatV2P.Outbox", "dbo.Stage2OriginalLogins" })
                    if (await Permission(table, "SELECT") != 0) throw Failure();
                foreach (var procedure in new[] { "dbo.sp_Stage2_CreateOriginalLogin", "dbo.sp_Auth_GetLoginCredential",
                    "dbo.sp_Stage2_ValidateOriginalLogin", "dbo.sp_Stage2_RevokeOriginalLogin",
                    "ChatV2P.Resolve", "ChatV2P.History", "ChatV2P.Own", "ChatV2P.Membership", "ChatV2P.Directory",
                    "ChatV2P.OpenPrivate", "ChatV2P.ExecuteIntent", "ChatV2P.Prepare", "ChatV2P.Leave" })
                {
                    bool expected = ExpectedPermission(role, procedure);
                    if (await Permission(procedure, "EXECUTE") != (expected ? 1 : 0)) throw Failure();
                }
                if (role == "ArchiveReader")
                    foreach (string procedure in new[] { "ChatV2Archive.Own", "ChatV2Archive.History" })
                        if (await Permission(procedure, "EXECUTE") != 1) throw Failure();
                if (role == "Pricing")
                    foreach (string procedure in new[] { "CommerceQuotes.Capture", "CommerceQuotes.Publish", "CommerceQuotes.ReadOwn" })
                        if (await Permission(procedure, "EXECUTE") != 1) throw Failure();
                async Task<int> Permission(string obj, string permission)
                {
                    await using var command = new SqlCommand("SELECT ISNULL(HAS_PERMS_BY_NAME(@obj,'OBJECT',@permission),0)", connection) { CommandTimeout = 5 };
                    command.Parameters.Add("@obj", System.Data.SqlDbType.NVarChar, 128).Value = obj;
                    command.Parameters.Add("@permission", System.Data.SqlDbType.NVarChar, 60).Value = permission;
                    return Convert.ToInt32(await command.ExecuteScalarAsync());
                }
            }
            catch (SqlException) { throw Failure(); }
        }
    }

    internal static bool ExpectedPermission(string role, string procedure) => procedure switch
    {
        "dbo.sp_Stage2_CreateOriginalLogin" => role == "Issuer",
        "dbo.sp_Stage2_ValidateOriginalLogin" or "dbo.sp_Stage2_RevokeOriginalLogin" => role is "App" or "MvcApp",
        "dbo.sp_Auth_GetLoginCredential" => role == "Auth",
        "ChatV2P.Resolve" or "ChatV2P.History" or "ChatV2P.Own" or "ChatV2P.Membership" or "ChatV2P.Directory" => role is "Reader" or "BrokerReader",
        "ChatV2P.OpenPrivate" or "ChatV2P.ExecuteIntent" or "ChatV2P.Prepare" or "ChatV2P.Leave" => role is "App" or "BrokerApp",
        _ => false
    };

    private static InvalidOperationException Failure() => new("No se acreditó el gate de privilegios SQL de la composición restringida.");
}
