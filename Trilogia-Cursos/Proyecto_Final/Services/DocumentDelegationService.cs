using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

public interface IDocumentDelegationService
{
    Task<DocumentDelegationsViewModel> ReadAsync(int page, CancellationToken ct);
    Task<long> SaveAsync(DocumentDelegationForm form, CancellationToken ct);
}
public sealed class DocumentDelegationService(IConfiguration configuration, IHttpContextAccessor accessor, BusinessClock clock)
    : IDocumentDelegationService
{
    private OriginalLoginContext Login() => OriginalLoginSession.ValidatedRequest(accessor.HttpContext
        ?? throw new UnauthorizedAccessException("Falta el contexto original acreditado."));
    private SqlConnection Connection()
    {
        _ = Login();
        if (!configuration.GetValue<bool>("Documents:DelegationEnabled")) throw new DocumentSourceUnavailableException();
        return new(configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Falta la identidad APP documental."));
    }
    public async Task<DocumentDelegationsViewModel> ReadAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(page));
        var login = Login(); var model = new DocumentDelegationsViewModel { Page = page };
        await using var connection = Connection(); await connection.OpenAsync(ct);
        using (var command = OriginalLoginService.ContextCommand(connection, "DocumentSecurity.GrantOptions", login))
        {
            command.CommandTimeout = 15; await using var reader = await command.ExecuteReaderAsync(ct);
            model.Departments = await Options(reader, ct);
            if (!await reader.NextResultAsync(ct)) throw new InvalidOperationException("Falta el catálogo autorizado de personas.");
            model.People = await Options(reader, ct); await Complete(reader, ct);
        }
        using (var command = OriginalLoginService.ContextCommand(connection, "DocumentSecurity.ListGrants", login))
        {
            command.CommandTimeout = 15; command.Parameters.Add("@Page", SqlDbType.Int).Value = page;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Falta el conteo de autorizaciones.");
            model.Total = reader.GetInt64(0);
            if (!await reader.NextResultAsync(ct)) throw new InvalidOperationException("Falta el listado de autorizaciones.");
            var rows = new List<DocumentDelegationRow>();
            while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), reader.GetInt64(6),
                DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc), reader.GetBoolean(8)));
            model.Grants = rows; await Complete(reader, ct);
        }
        return model;
    }
    public async Task<long> SaveAsync(DocumentDelegationForm form, CancellationToken ct)
    {
        if ((string.IsNullOrEmpty(form.Operation) || !DocumentOperations.Labels.ContainsKey(form.Operation)) || (form.Enabled && form.ValidThrough is null))
            throw new ArgumentException("Operación o vencimiento de autorización incompletos.", nameof(form));
        var login = Login(); await using var connection = Connection();
        using var command = OriginalLoginService.ContextCommand(connection, "DocumentSecurity.SetGrant", login);
        command.CommandTimeout = 15;
        command.Parameters.Add("@Department", SqlDbType.Int).Value = form.Department;
        command.Parameters.Add("@Grantee", SqlDbType.Int).Value = form.Grantee;
        command.Parameters.Add("@Operation", SqlDbType.VarChar, 10).Value = form.Operation;
        command.Parameters.Add("@Enabled", SqlDbType.Bit).Value = form.Enabled;
        command.Parameters.Add("@ExpectedRevision", SqlDbType.BigInt).Value = form.ExpectedRevision;
        command.Parameters.Add(new SqlParameter("@GrantExpiresUtc", SqlDbType.DateTime2)
        { Scale = 7, Value = form.Enabled && form.ValidThrough is DateTime date ? clock.EndOfDayExclusiveUtc(date) : DBNull.Value });
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = form.Reason.Trim();
        await connection.OpenAsync(ct); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Falta la revisión confirmada de la autorización.");
        long revision = reader.GetInt64(0); await Complete(reader, ct); return revision;
    }
    private static async Task<IReadOnlyList<SelectOptionViewModel>> Options(SqlDataReader reader, CancellationToken ct)
    {
        var values = new List<SelectOptionViewModel>();
        while (await reader.ReadAsync(ct)) values.Add(new() { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        return values;
    }
    private static async Task Complete(SqlDataReader reader, CancellationToken ct)
    { while (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) { } }
}
