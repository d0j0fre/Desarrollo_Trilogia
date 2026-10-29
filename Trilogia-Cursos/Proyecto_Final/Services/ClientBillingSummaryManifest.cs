using System.Data;
using Microsoft.Data.SqlClient;
namespace Proyecto_Final.Services;
public static class ClientBillingSummaryManifest
{
    public const string Migration = "0053_stage2_client_billing_summary";
    public const string MigrationHash = "61411083D71E6C0A98A338AB72603030D55CADC1EDC1DA73304AB8EE48084D0C";
    public const string ModuleHash = "F2B965FF754AB271D5ABCCEA7A19F35D3E198BC88AEFB66CC3C846687461F22D";
    public static async Task VerifyAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand("""
            SELECT COUNT(*) FROM sys.sql_modules m
            WHERE m.object_id=OBJECT_ID(N'dbo.sp_Stage2_ClientBillingSummary')
            AND CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2)=@module
            AND m.uses_ansi_nulls=1 AND m.uses_quoted_identifier=1
            AND HAS_PERMS_BY_NAME(N'dbo.sp_Stage2_ClientBillingSummary','OBJECT','EXECUTE')=1
            AND ISNULL(HAS_PERMS_BY_NAME(N'dbo.sp_Stage2_ClientBillingSummary','OBJECT','ALTER'),0)=0
            AND EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=@migration AND FileSha256=@hash AND Status=N'Applied');
            """, connection) { CommandTimeout = 5 };
        command.Parameters.Add("@module", SqlDbType.VarChar, 64).Value = ModuleHash;
        command.Parameters.Add("@migration", SqlDbType.NVarChar, 300).Value = Migration;
        command.Parameters.Add("@hash", SqlDbType.VarChar, 64).Value = MigrationHash;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("No se acreditó el resumen SQL de facturación del cliente.");
    }
}
