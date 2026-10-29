using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services.ChatV2;

/// <summary>Personal Chat V2 using canonical OriginalLogin, never ChatCandidate sessions or legacy chat.</summary>
public sealed class SqlPersonalChatV2Persistence(IConfiguration configuration, IDataProtectionProvider protection,
    TimeProvider clock) : IChatV2IntentPersistence
{
    private readonly IDataProtector _cursors = protection.CreateProtector("ChatV2P.History.v1");
    private sealed record Cursor(Guid Login, int Actor, int Resource, long ActorEpoch, long ResourceEpoch,
        long After, long Upper, DateTimeOffset Expires);

    private SqlConnection Connection(bool read)
    {
        var value = configuration.GetConnectionString(read ? "ChatV2Reader" : "ChatV2App");
        if (string.IsNullOrWhiteSpace(value)) throw new ChatV2Exception(ChatV2Failure.Dependency);
        var settings = new SqlConnectionStringBuilder(value);
        // Restricted SQL principals: contained users or explicit login/user mappings, never bootstrap credentials.
        if (settings.IntegratedSecurity || string.IsNullOrWhiteSpace(settings.UserID) ||
            string.IsNullOrWhiteSpace(settings.InitialCatalog) || settings.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase))
            throw new ChatV2Exception(ChatV2Failure.Dependency);
        return new(value);
    }
    private static void Personal(ChatV2Resource resource)
    {
        resource.Validate();
        if (resource.Domain != ChatV2Domain.Private) throw new ChatV2Exception(ChatV2Failure.Dependency);
    }
    private static ChatV2Exception Failure(SqlException ex) => new(ex.Number switch
    {
        56801 or 56601 => ChatV2Failure.Session,
        56802 or 58402 => ChatV2Failure.Forbidden,
        58404 => ChatV2Failure.Resource,
        58403 => ChatV2Failure.Conflict,
        _ => ChatV2Failure.Dependency
    });
    private static void Resource(SqlCommand command, ChatV2Resource resource)
    {
        Personal(resource); command.Parameters.Add("@ConversationId", SqlDbType.Int).Value = resource.Id;
    }
    private async ValueTask<T> Query<T>(OriginalLoginContext login, string operation, bool read,
        Action<SqlCommand>? parameters, Func<SqlDataReader, CancellationToken, Task<T>> map, CancellationToken ct)
    {
        if (!login.HasCanonicalLifetime || login.ExpiresUtc <= clock.GetUtcNow().UtcDateTime)
            throw new ChatV2Exception(ChatV2Failure.Session);
        try
        {
            await using var connection = Connection(read);
            await using var command = OriginalLoginService.ContextCommand(connection, "ChatV2P." + operation, login);
            command.CommandTimeout = 15;
            parameters?.Invoke(command);
            await connection.OpenAsync(ct);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var result = await map(reader, ct);
            // Consume completion/COMMIT errors; a result row alone is not evidence of a committed operation.
            while (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) { }
            return result;
        }
        catch (SqlException exception) { throw Failure(exception); }
    }
    private static DateTime Utc(SqlDataReader r, int ordinal) => DateTime.SpecifyKind(r.GetDateTime(ordinal), DateTimeKind.Utc);
    public ValueTask<ChatV2LiveIdentity> ResolveAsync(OriginalLoginContext login, CancellationToken ct) =>
        Query(login, "Resolve", true, null, async (r, token) =>
        {
            if (!await r.ReadAsync(token)) throw new ChatV2Exception(ChatV2Failure.Session);
            return new ChatV2LiveIdentity(login.LoginId, login.LoginId, login.UserId, login.ProfileId,
                r.GetBoolean(1) ? ChatV2Kind.Client : ChatV2Kind.Internal, login.SecurityStamp, r.GetInt64(0),
                new DateTimeOffset(login.ExpiresUtc), r.GetBoolean(2) ? ChatV2Capabilities.InternalUse : ChatV2Capabilities.None);
        }, ct);

    public ValueTask<ChatV2IntentEpochs> PrepareAsync(OriginalLoginContext login, ChatV2LiveIdentity identity,
        ChatV2Resource resource, Guid request, string contentHash, CancellationToken ct) =>
        Query(login, "Prepare", false, c =>
        {
            Resource(c, resource);
            c.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = request;
            c.Parameters.Add("@ContentHash", SqlDbType.Binary, 32).Value = Convert.FromHexString(contentHash);
            c.Parameters.Add("@ActorEpoch", SqlDbType.BigInt).Value = identity.ActorEpoch;
        }, async (r, token) =>
        {
            if (!await r.ReadAsync(token)) throw new ChatV2Exception(ChatV2Failure.Contract);
            return new ChatV2IntentEpochs(r.GetInt64(0), r.GetInt64(1), new DateTimeOffset(Utc(r, 2)));
        }, ct);

    public ValueTask<ChatV2Receipt> ExecuteAsync(ChatV2BoundIntent intent, string? content, bool receiptOnly, CancellationToken ct) =>
        Query(intent.Login, "ExecuteIntent", false, c =>
        {
            Resource(c, intent.Resource);
            c.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = intent.Request;
            c.Parameters.Add("@ContentHash", SqlDbType.Binary, 32).Value = Convert.FromHexString(intent.ContentHash);
            c.Parameters.Add("@ActorEpoch", SqlDbType.BigInt).Value = intent.Identity.ActorEpoch;
            c.Parameters.Add("@ResourceEpoch", SqlDbType.BigInt).Value = intent.ResourceEpoch;
            c.Parameters.Add(new SqlParameter("@IntentExpiresUtc", SqlDbType.DateTime2) { Scale = 7, Value = intent.ExpiresUtc.UtcDateTime });
            c.Parameters.Add("@ReceiptOnly", SqlDbType.Bit).Value = receiptOnly;
            c.Parameters.Add("@Content", SqlDbType.NVarChar, 1000).Value = content is null ? DBNull.Value : content;
        }, async (r, token) =>
        {
            if (!await r.ReadAsync(token)) throw new ChatV2Exception(ChatV2Failure.Contract);
            return new ChatV2Receipt(intent.Request, ChatV2Domain.Private, intent.Resource.Id, r.GetInt64(0),
                r.GetInt64(1), Utc(r, 2), "PERSISTED", r.GetString(3), intent.ResourceEpoch);
        }, ct);

    public ValueTask<IReadOnlyList<ChatV2OwnedResource>> ListAsync(OriginalLoginContext login, ChatV2Domain domain, CancellationToken ct)
    {
        Personal(new(domain, 1));
        return Query<IReadOnlyList<ChatV2OwnedResource>>(login, "Own", true, null, async (r, token) =>
        {
            var rows = new List<ChatV2OwnedResource>();
            while (await r.ReadAsync(token)) rows.Add(new(ChatV2Domain.Private, r.GetInt32(0), r.GetInt64(1)));
            return rows;
        }, ct);
    }
    public ValueTask<ChatV2ResourceAccess> MembershipAsync(OriginalLoginContext login, ChatV2Resource resource, CancellationToken ct) =>
        Query(login, "Membership", true, c => Resource(c, resource), async (r, token) =>
        {
            if (!await r.ReadAsync(token)) throw new ChatV2Exception(ChatV2Failure.Resource);
            return new ChatV2ResourceAccess(true, true, r.GetInt64(0));
        }, ct);

    public async ValueTask<ChatV2HistoryResponse> HistoryAsync(OriginalLoginContext login, ChatV2Resource resource, string? cursor, CancellationToken ct)
    {
        Personal(resource);
        var actor = await ResolveAsync(login, ct);
        Cursor? previous = null;
        if (cursor is not null)
        {
            if (cursor.Length > 2048) throw new ChatV2Exception(ChatV2Failure.Validation);
            try { previous = JsonSerializer.Deserialize<Cursor>(_cursors.Unprotect(cursor)); }
            catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
            { throw new ChatV2Exception(ChatV2Failure.Conflict); }
            if (previous is null || previous.Login != login.LoginId || previous.Actor != login.UserId ||
                previous.Resource != resource.Id || previous.ActorEpoch != actor.ActorEpoch || previous.Expires <= clock.GetUtcNow())
                throw new ChatV2Exception(ChatV2Failure.Conflict);
        }
        var page = await Query(login, "History", true, c =>
        {
            Resource(c, resource);
            c.Parameters.Add("@After", SqlDbType.BigInt).Value = previous?.After ?? 0;
            c.Parameters.Add("@Upper", SqlDbType.BigInt).Value = previous is null ? DBNull.Value : previous.Upper;
            c.Parameters.Add("@ActorEpoch", SqlDbType.BigInt).Value = actor.ActorEpoch;
            c.Parameters.Add("@ResourceEpoch", SqlDbType.BigInt).Value = previous is null ? DBNull.Value : previous.ResourceEpoch;
        }, async (r, token) =>
        {
            var rows = new List<ChatV2Message>();
            while (await r.ReadAsync(token)) rows.Add(new(r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), r.GetString(3), null, Utc(r, 4), null, null));
            if (!await r.NextResultAsync(token) || !await r.ReadAsync(token)) throw new ChatV2Exception(ChatV2Failure.Contract);
            return (Rows: rows, Upper: r.GetInt64(0), Version: r.GetInt64(1));
        }, ct);
        var after = await ResolveAsync(login, ct);
        var access = await MembershipAsync(login, resource, ct);
        if (after.ActorEpoch != actor.ActorEpoch || access.AuthorizationVersion != page.Version)
            throw new ChatV2Exception(ChatV2Failure.Resource);
        var rows = page.Rows.Take(50).ToArray();
        var expires = clock.GetUtcNow().AddMinutes(15);
        if (expires.UtcDateTime > login.ExpiresUtc) expires = new(login.ExpiresUtc);
        var next = page.Rows.Count > 50 ? _cursors.Protect(JsonSerializer.Serialize(new Cursor(login.LoginId,
            login.UserId, resource.Id, actor.ActorEpoch, page.Version, rows[^1].StreamSequence, page.Upper, expires))) : null;
        return new(rows, next);
    }
    public ValueTask<IReadOnlyList<ChatV2Contact>> DirectoryAsync(OriginalLoginContext login, CancellationToken ct) =>
        Query<IReadOnlyList<ChatV2Contact>>(login, "Directory", true, null, async (r, token) =>
        {
            var rows = new List<ChatV2Contact>();
            while (await r.ReadAsync(token)) rows.Add(new(r.GetInt32(0)));
            return rows;
        }, ct);
    public ValueTask<int> OpenPrivateAsync(OriginalLoginContext login, int otherUser, CancellationToken ct) =>
        Query(login, "OpenPrivate", false, c => c.Parameters.Add("@OtherUser", SqlDbType.Int).Value = otherUser,
            async (r, token) => { if (!await r.ReadAsync(token)) throw new ChatV2Exception(ChatV2Failure.Resource); return r.GetInt32(0); }, ct);
    public async ValueTask LeaveAsync(OriginalLoginContext login, ChatV2Resource resource, CancellationToken ct) =>
        _ = await Query(login, "Leave", false, c => Resource(c, resource), (_, _) => Task.FromResult(true), ct);
}
