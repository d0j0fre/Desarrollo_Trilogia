using System.Text.Json;
using System.Security.Cryptography;

namespace Proyecto_Final.Services.ChatV2.Broker;

/// <summary>No SQL credentials or fallback. The exchange authenticates both Windows endpoints before sending context.</summary>
internal sealed class BrokerChatV2Persistence(IChatV2BrokerExchange exchange) : IChatV2IntentPersistence
{
    private async ValueTask<T> Call<T>(BrokerRequest request, CancellationToken ct)
    {
        try
        {
        var response = await exchange.ExchangeAsync(request, ct);
        if (response.Version != 1 || response.Correlation != request.Correlation) throw new ChatV2Exception(ChatV2Failure.Contract);
        if (response.Failure is ChatV2Failure failure)
        {
            if (!Enum.IsDefined(failure) || response.Result is not null) throw new ChatV2Exception(ChatV2Failure.Contract);
            throw new ChatV2Exception(failure);
        }
        try
        {
            if (response.Result is not JsonElement element) throw new ChatV2Exception(ChatV2Failure.Contract);
            var result = element.Deserialize<T>(BrokerFrame.Json);
            return result is null ? throw new ChatV2Exception(ChatV2Failure.Contract) : result;
        }
        catch (JsonException) { throw new ChatV2Exception(ChatV2Failure.Contract); }
        }
        finally { CryptographicOperations.ZeroMemory(request.Login.BindingHash); }
    }
    private static BrokerRequest Request(BrokerOperation operation, OriginalLoginContext login)
        => new(1, Guid.NewGuid(), operation, BrokerLogin.Capture(login));
    public ValueTask<ChatV2LiveIdentity> ResolveAsync(OriginalLoginContext login, CancellationToken ct)
        => Call<ChatV2LiveIdentity>(Request(BrokerOperation.Resolve, login), ct);
    public ValueTask<IReadOnlyList<ChatV2Contact>> DirectoryAsync(OriginalLoginContext login, CancellationToken ct)
        => Call<IReadOnlyList<ChatV2Contact>>(Request(BrokerOperation.Directory, login), ct);
    public ValueTask<int> OpenPrivateAsync(OriginalLoginContext login, int otherUser, CancellationToken ct)
        => Call<int>(Request(BrokerOperation.OpenPrivate, login) with { OtherUser = otherUser }, ct);
    public ValueTask<IReadOnlyList<ChatV2OwnedResource>> ListAsync(OriginalLoginContext login, ChatV2Domain domain, CancellationToken ct)
        => Call<IReadOnlyList<ChatV2OwnedResource>>(Request(BrokerOperation.List, login) with { Resource = new(domain, 1) }, ct);
    public ValueTask<ChatV2ResourceAccess> MembershipAsync(OriginalLoginContext login, ChatV2Resource resource, CancellationToken ct)
        => Call<ChatV2ResourceAccess>(Request(BrokerOperation.Membership, login) with { Resource = resource }, ct);
    public ValueTask<ChatV2HistoryResponse> HistoryAsync(OriginalLoginContext login, ChatV2Resource resource, string? cursor, CancellationToken ct)
        => Call<ChatV2HistoryResponse>(Request(BrokerOperation.History, login) with { Resource = resource, Cursor = cursor }, ct);
    public ValueTask<ChatV2IntentEpochs> PrepareAsync(OriginalLoginContext login, ChatV2LiveIdentity identity,
        ChatV2Resource resource, Guid request, string contentHash, CancellationToken ct)
        => Call<ChatV2IntentEpochs>(Request(BrokerOperation.Prepare, login) with
        { Resource = resource, RequestId = request, ContentHash = contentHash, ActorEpoch = identity.ActorEpoch }, ct);
    public ValueTask<ChatV2Receipt> ExecuteAsync(ChatV2BoundIntent intent, string? content, bool receiptOnly, CancellationToken ct)
        => Call<ChatV2Receipt>(Request(receiptOnly ? BrokerOperation.Receipt : BrokerOperation.Send, intent.Login) with
        { Resource = intent.Resource, RequestId = intent.Request, ContentHash = intent.ContentHash,
            ActorEpoch = intent.Identity.ActorEpoch, ResourceEpoch = intent.ResourceEpoch,
            IntentExpiresUtc = intent.ExpiresUtc, Content = content }, ct);
    public async ValueTask LeaveAsync(OriginalLoginContext login, ChatV2Resource resource, CancellationToken ct)
        => _ = await Call<bool>(Request(BrokerOperation.Leave, login) with { Resource = resource }, ct);
    public ValueTask<IReadOnlyList<ChatV2QueueItem>> QueueAsync(OriginalLoginContext login, CancellationToken ct)
        => Call<IReadOnlyList<ChatV2QueueItem>>(Request(BrokerOperation.Queue, login), ct);
    public async ValueTask<long> TransferAsync(OriginalLoginContext login, int caseId, int newAgent, long expectedVersion, CancellationToken ct)
    {
        var result = await Call<long>(Request(BrokerOperation.Transfer, login) with
        { AttentionTransfer = new(caseId, newAgent, expectedVersion) }, ct);
        if (expectedVersion < 0 || expectedVersion == long.MaxValue || result != expectedVersion + 1)
            throw new ChatV2Exception(ChatV2Failure.Contract);
        return result;
    }
}
