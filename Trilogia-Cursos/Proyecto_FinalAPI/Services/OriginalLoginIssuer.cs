using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services;

public sealed record OriginalLoginTicket(Guid LoginId, DateTime IssuedUtc, DateTime ExpiresUtc);

public interface IOriginalLoginIssuer
{
    Task<OriginalLoginTicket> IssueAsync(ApiUser authenticatedUser, byte[] bindingHash, CancellationToken cancellationToken);
}

/// <summary>Credential authority only. MVC must never receive this SQL credential.</summary>
public sealed class OriginalLoginIssuer(IConfiguration configuration) : IOriginalLoginIssuer
{
    public async Task<OriginalLoginTicket> IssueAsync(ApiUser user, byte[] bindingHash, CancellationToken cancellationToken)
    {
        if (!user.Activo || !user.PerfilActivo || user.UsuarioId <= 0 || user.PerfilId <= 0
            || !Guid.TryParse(user.SecurityStamp, out var stamp) || stamp == Guid.Empty
            || bindingHash.Length != 32 || bindingHash.All(value => value == 0))
            throw new InvalidOperationException("No se acreditó la identidad autenticada.");

        await using var connection = new SqlConnection(configuration.GetConnectionString("OriginalLoginIssuer")
            ?? throw new InvalidOperationException("Falta la identidad de emisión."));
        await using var command = new SqlCommand("dbo.sp_Stage2_CreateOriginalLogin", connection)
        { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = user.UsuarioId;
        command.Parameters.Add("@PerfilId", SqlDbType.Int).Value = user.PerfilId;
        command.Parameters.Add("@SecurityStamp", SqlDbType.UniqueIdentifier).Value = stamp;
        command.Parameters.Add("@BindingHash", SqlDbType.Binary, 32).Value = bindingHash;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("No se acreditó la emisión.");
        var ticket = new OriginalLoginTicket(reader.GetGuid(0),
            DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
            DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc));
        while (await reader.NextResultAsync(cancellationToken)) while (await reader.ReadAsync(cancellationToken)) { }
        if (ticket.LoginId == Guid.Empty || ticket.ExpiresUtc - ticket.IssuedUtc != TimeSpan.FromHours(8))
            throw new InvalidOperationException("Vigencia de emisión no acreditada.");
        return ticket;
    }
}

public static class OriginalLoginAuthorityConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("OriginalLogin:AuthorityEnabled")) return;
        var auth = Read("DefaultConnection");
        var issuer = Read("OriginalLoginIssuer");
        if (auth.UserID.Equals(issuer.UserID, StringComparison.OrdinalIgnoreCase)
            || !auth.DataSource.Equals(issuer.DataSource, StringComparison.OrdinalIgnoreCase)
            || !auth.InitialCatalog.Equals(issuer.InitialCatalog, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("ChatV2App"))
            || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("ChatV2Reader")))
            throw Invalid();
        SqlConnectionStringBuilder Read(string name)
        {
            try
            {
                var value = new SqlConnectionStringBuilder(configuration.GetConnectionString(name) ?? "");
                if (value.IntegratedSecurity || value.PersistSecurityInfo || string.IsNullOrWhiteSpace(value.UserID)
                    || value.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value.Password)
                    || string.IsNullOrWhiteSpace(value.DataSource) || string.IsNullOrWhiteSpace(value.InitialCatalog)) throw Invalid();
                return value;
            }
            catch (ArgumentException) { throw Invalid(); }
        }
    }
    private static InvalidOperationException Invalid() => new("La autoridad de login requiere identidades Auth/Issuer restringidas y un destino acreditado.");
}
