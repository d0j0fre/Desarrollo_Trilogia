using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Chat;

namespace Proyecto_Final.Services.ChatV2;

/// <summary>Read-only historical personal messages, bound to the canonical original login.
/// The private SQL candidate must be accredited before this reader can be enabled.</summary>
public sealed class SqlLegacyPersonalArchive(IConfiguration configuration)
{
    public async Task<IReadOnlyList<int>> ListAsync(OriginalLoginContext login, CancellationToken ct)
        => await Query(login, "Own", null, async (reader, token) =>
        {
            var ids = new List<int>();
            while (await reader.ReadAsync(token)) ids.Add(reader.GetInt32(0));
            return ids;
        }, ct);

    public async Task<IReadOnlyList<ChatMessageViewModel>> ReadAsync(OriginalLoginContext login,
        int conversationId, int page, int pageSize, CancellationToken ct)
        => await Query(login, "History", command =>
        {
            if (conversationId <= 0) throw new ChatV2Exception(ChatV2Failure.Resource);
            command.Parameters.Add("@ConversationId", SqlDbType.Int).Value = conversationId;
            command.Parameters.Add("@Page", SqlDbType.Int).Value = Math.Max(page, 1);
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = Math.Clamp(pageSize, 1, 100);
        }, async (reader, token) =>
        {
            var rows = new List<ChatMessageViewModel>();
            while (await reader.ReadAsync(token)) rows.Add(new()
            {
                MensajeId = reader.GetInt32(0), ConversacionId = reader.GetInt32(1),
                RemitenteId = reader.GetInt32(2), Contenido = reader.GetString(3),
                // Historical timestamps are preserved; no UTC provenance is invented.
                FechaEnvio = reader.GetDateTime(4), Leido = reader.GetBoolean(5)
            });
            return rows;
        }, ct);

    private async Task<T> Query<T>(OriginalLoginContext login, string operation,
        Action<SqlCommand>? parameters, Func<SqlDataReader, CancellationToken, Task<T>> read, CancellationToken ct)
    {
        var value = configuration.GetConnectionString("ChatV2Reader");
        if (string.IsNullOrWhiteSpace(value)) throw new ChatV2Exception(ChatV2Failure.Dependency);
        var settings = new SqlConnectionStringBuilder(value);
        if (settings.IntegratedSecurity || string.IsNullOrWhiteSpace(settings.UserID)
            || string.IsNullOrWhiteSpace(settings.InitialCatalog) || settings.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase))
            throw new ChatV2Exception(ChatV2Failure.Dependency);
        try
        {
            await using var connection = new SqlConnection(value);
            await using var command = OriginalLoginService.ContextCommand(connection, "ChatV2Archive." + operation, login);
            command.CommandTimeout = 15;
            parameters?.Invoke(command);
            await connection.OpenAsync(ct);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var result = await read(reader, ct);
            while (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) { }
            return result;
        }
        catch (SqlException ex)
        {
            throw new ChatV2Exception(ex.Number switch
            {
                56801 => ChatV2Failure.Session, 56802 => ChatV2Failure.Forbidden,
                58504 => ChatV2Failure.Resource, _ => ChatV2Failure.Dependency
            });
        }
    }
}
