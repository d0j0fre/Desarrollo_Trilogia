using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proyecto_Final.Services.ChatV2.Broker;

// Infrastructure DTOs only. Never bind these types in an HTTP action or a Hub.
internal enum BrokerOperation { Resolve, Directory, OpenPrivate, List, Membership, History, Prepare, Send, Receipt, Leave, Queue, Transfer }
internal sealed record BrokerAttentionTransfer(int CaseId, int NewAgent, long ExpectedVersion);
internal sealed record BrokerLogin(Guid LoginId, int UserId, int ProfileId, Guid SecurityStamp,
    DateTime IssuedUtc, DateTime ExpiresUtc, byte[] BindingHash)
{
    public static BrokerLogin Capture(OriginalLoginContext value) => new(value.LoginId, value.UserId,
        value.ProfileId, value.SecurityStamp, value.IssuedUtc, value.ExpiresUtc, value.BindingHash.ToArray());
    public OriginalLoginContext Restore()
    {
        if (BindingHash is not { Length: 32 }) throw new ChatV2Exception(ChatV2Failure.Session);
        var login = new OriginalLoginContext(LoginId, UserId, ProfileId, SecurityStamp, IssuedUtc, ExpiresUtc, BindingHash);
        if (!login.HasCanonicalLifetime) throw new ChatV2Exception(ChatV2Failure.Session);
        return login;
    }
    public override string ToString() => nameof(BrokerLogin);
}
internal sealed record BrokerRequest(int Version, Guid Correlation, BrokerOperation Operation, BrokerLogin Login,
    ChatV2Resource? Resource = null, int? OtherUser = null, Guid? RequestId = null, string? ContentHash = null,
    long? ActorEpoch = null, long? ResourceEpoch = null, DateTimeOffset? IntentExpiresUtc = null,
    string? Content = null, string? Cursor = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BrokerAttentionTransfer? AttentionTransfer = null)
{
    public override string ToString() => nameof(BrokerRequest);
}
internal sealed record BrokerResponse(int Version, Guid Correlation, ChatV2Failure? Failure, JsonElement? Result)
{
    public static BrokerResponse Success<T>(Guid correlation, T result) => new(1, correlation, null,
        JsonSerializer.SerializeToElement(result, BrokerFrame.Json));
    public override string ToString() => nameof(BrokerResponse);
}

internal static class BrokerFrame
{
    internal const int RequestLimit = 16 * 1024, ResponseLimit = 384 * 1024;
    internal static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };
    internal static async ValueTask WriteAsync<T>(Stream stream, T value, int limit, CancellationToken ct)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        try
        {
            if (bytes.Length == 0 || bytes.Length > limit) throw new InvalidDataException("Broker frame size.");
            byte[] prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, bytes.Length);
            await stream.WriteAsync(prefix, ct); await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal static async ValueTask<T> ReadAsync<T>(Stream stream, int limit, CancellationToken ct)
    {
        byte[] prefix = new byte[4]; await stream.ReadExactlyAsync(prefix, ct);
        int length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length <= 0 || length > limit) throw new InvalidDataException("Broker frame size.");
        byte[] bytes = new byte[length];
        try
        {
            await stream.ReadExactlyAsync(bytes, ct);
            return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException("Broker frame null.");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
