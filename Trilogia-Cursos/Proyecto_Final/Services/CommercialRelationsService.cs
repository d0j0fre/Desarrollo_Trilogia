using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

public interface ICommercialRelationsService
{
    Task<CommercialRelationsViewModel> ReadAsync(int page, CancellationToken cancellationToken);
    Task<long> SaveAsync(string kind, CommercialRelationForm input, CancellationToken cancellationToken);
}

public sealed class CommercialRelationsService(IConfiguration configuration, IHttpContextAccessor http, BusinessClock clock)
    : ICommercialRelationsService
{
    private OriginalLoginContext Login() => OriginalLoginSession.ValidatedRequest(
        http.HttpContext ?? throw new UnauthorizedAccessException("Falta el contexto original validado."));

    private SqlConnection Connection() => new(configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Falta DefaultConnection."));

    public async Task<CommercialRelationsViewModel> ReadAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(page));
        var login = Login();
        var model = new CommercialRelationsViewModel { Page = page };
        await using var connection = Connection();
        await connection.OpenAsync(ct);
        await using (var command = OriginalLoginService.ContextCommand(connection, "CommercialSecurity.Options", login))
        {
            command.CommandTimeout = 15;
            await using var reader = await command.ExecuteReaderAsync(ct);
            model.Sellers = await People(reader, ct);
            if (!await reader.NextResultAsync(ct)) throw new InvalidOperationException("Falta el catálogo autorizado de clientes.");
            model.Customers = await People(reader, ct);
            if (!await reader.NextResultAsync(ct)) throw new InvalidOperationException("Falta el catálogo autorizado de supervisores.");
            model.Supervisors = await People(reader, ct);
            await Complete(reader, ct);
        }
        await using (var command = OriginalLoginService.ContextCommand(connection, "CommercialSecurity.List", login))
        {
            command.CommandTimeout = 15;
            command.Parameters.Add("@Page", SqlDbType.Int).Value = page;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Falta el conteo de relaciones.");
            model.Total = reader.GetInt64(0);
            if (!await reader.NextResultAsync(ct)) throw new InvalidOperationException("Falta el listado de relaciones.");
            var rows = new List<CommercialRelationRow>();
            while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetString(4), reader.GetBoolean(5), reader.GetInt64(6),
                reader.IsDBNull(7) ? null : DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc), reader.GetBoolean(8)));
            await Complete(reader, ct);
            model.Relations = rows;
        }
        return model;
    }

    public async Task<long> SaveAsync(string kind, CommercialRelationForm input, CancellationToken ct)
    {
        if (kind is not ("SC" or "SS")) throw new ArgumentException("Tipo de relación no válido.", nameof(kind));
        var login = Login();
        await using var connection = Connection();
        await using var command = OriginalLoginService.ContextCommand(connection, "CommercialSecurity.SetAssignment", login);
        command.CommandTimeout = 15;
        command.Parameters.Add("@Kind", SqlDbType.Char, 2).Value = kind;
        command.Parameters.Add("@ParentUser", SqlDbType.Int).Value = input.ParentUser;
        command.Parameters.Add("@ChildUser", SqlDbType.Int).Value = input.ChildUser;
        command.Parameters.Add("@Enabled", SqlDbType.Bit).Value = input.Enabled;
        command.Parameters.Add("@ExpectedRevision", SqlDbType.BigInt).Value = input.ExpectedRevision;
        command.Parameters.Add(new SqlParameter("@RelationshipExpiresUtc", SqlDbType.DateTime2)
        {
            Scale = 7, Value = input.Enabled && input.ValidThrough is DateTime date
                ? clock.EndOfDayExclusiveUtc(date) : DBNull.Value
        });
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = input.Reason.Trim();
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Falta la revisión confirmada.");
        var revision = reader.GetInt64(0);
        await Complete(reader, ct);
        return revision;
    }

    private static async Task<List<CommercialPersonOption>> People(SqlDataReader reader, CancellationToken ct)
    {
        var people = new List<CommercialPersonOption>();
        while (await reader.ReadAsync(ct)) people.Add(new(reader.GetInt32(0), reader.GetString(1)));
        return people;
    }
    private static async Task Complete(SqlDataReader reader, CancellationToken ct)
    { while (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) { } }
}
