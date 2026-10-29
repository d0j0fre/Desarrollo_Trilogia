using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services;

internal static class DocumentSqlContext
{
    internal static SqlCommand Command(SqlConnection connection, string operation, OriginalLoginContext login)
    {
        if (!login.HasCanonicalLifetime) throw new UnauthorizedAccessException("Falta el login original acreditado.");
        if (!DocumentDelegationManifest.Operations.Contains(operation, StringComparer.Ordinal))
            throw new ArgumentException("Operación documental no acreditada.", nameof(operation));
        var command = new SqlCommand("DocumentSecurity." + operation, connection)
            { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
        // Separate names avoid colliding with the historical @UsuarioId write-actor parameter.
        command.Parameters.Add("@OriginalLoginId", SqlDbType.UniqueIdentifier).Value = login.LoginId;
        command.Parameters.Add("@ContextActor", SqlDbType.Int).Value = login.UserId;
        command.Parameters.Add("@ContextProfile", SqlDbType.Int).Value = login.ProfileId;
        command.Parameters.Add("@ContextStamp", SqlDbType.UniqueIdentifier).Value = login.SecurityStamp;
        command.Parameters.Add("@ContextBinding", SqlDbType.Binary, 32).Value = login.BindingHash.ToArray();
        command.Parameters.Add(new SqlParameter("@ContextIssuedUtc", SqlDbType.DateTime2) { Scale = 7, Value = login.IssuedUtc });
        command.Parameters.Add(new SqlParameter("@ContextExpiresUtc", SqlDbType.DateTime2) { Scale = 7, Value = login.ExpiresUtc });
        command.Parameters.Add("@Actor", SqlDbType.Int).Value = login.UserId;
        return command;
    }
}
