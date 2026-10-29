using Proyecto_Final.Services;
using Proyecto_Final.Services.ChatV2;
using Proyecto_Final.Services.ChatV2.Broker;

namespace Proyecto_Final.Tests;

public sealed class ChatV2BrokerAdapterTests
{
    private static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 7, 2, Guid.NewGuid(), issued, issued.AddHours(8), Enumerable.Repeat((byte)42, 32).ToArray());
    }
    [Fact]
    public async Task The_original_session_and_payload_cross_the_port_without_creating_a_new_login()
    {
        var login = Login(); var uuid = Guid.NewGuid(); var exchange = new Fake((request, _) =>
        {
            Assert.Equal(login.LoginId, request.Login.LoginId); Assert.Equal(login.BindingHash.ToArray(), request.Login.BindingHash);
            Assert.Equal(uuid, request.RequestId); Assert.Equal("original", request.Content);
            Assert.Equal(3, request.ResourceEpoch); Assert.Equal(BrokerOperation.Send, request.Operation);
            return ValueTask.FromResult(BrokerResponse.Success(request.Correlation, new ChatV2Receipt(uuid,
                ChatV2Domain.Private, 5, 1, 1, DateTime.UtcNow, "PERSISTED", "original", 3)));
        });
        var adapter = new BrokerChatV2Persistence(exchange);
        var identity = new ChatV2LiveIdentity(login.LoginId, login.LoginId, login.UserId, login.ProfileId,
            ChatV2Kind.Internal, login.SecurityStamp, 7, new(login.ExpiresUtc), ChatV2Capabilities.InternalUse);
        var bound = new ChatV2BoundIntent(login, identity, "opaque", new(ChatV2Domain.Private, 5), uuid,
            ChatV2Continuity.ContentHash("original"), 3, DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Equal(uuid, (await adapter.ExecuteAsync(bound, "original", false, default)).RequestId);
        Assert.Equal(1, exchange.Calls); Assert.All(exchange.Last!.Login.BindingHash, b => Assert.Equal(0, b));
        Assert.All(login.BindingHash.ToArray(), b => Assert.Equal(42, b));
    }
    [Theory]
    [InlineData((int)ChatV2Failure.Resource)] [InlineData((int)ChatV2Failure.Session)]
    [InlineData((int)ChatV2Failure.Forbidden)] [InlineData((int)ChatV2Failure.Conflict)]
    public async Task Opaque_denials_keep_their_contract_without_retry_or_SQL_fallback(int failure)
    {
        var exchange = new Fake((request, _) => ValueTask.FromResult(new BrokerResponse(1, request.Correlation, (ChatV2Failure)failure, null)));
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => new BrokerChatV2Persistence(exchange).ResolveAsync(Login(), default).AsTask());
        Assert.Equal((ChatV2Failure)failure, error.Failure); Assert.Equal(1, exchange.Calls);
    }
    [Fact]
    public async Task Infrastructure_failure_does_not_retry_and_clears_the_transport_binding()
    {
        var exchange = new Fake((_, _) => throw new IOException());
        await Assert.ThrowsAsync<IOException>(() => new BrokerChatV2Persistence(exchange).ResolveAsync(Login(), default).AsTask());
        Assert.Equal(1, exchange.Calls); Assert.All(exchange.Last!.Login.BindingHash, b => Assert.Equal(0, b));
    }
    [Fact]
    public async Task A_cancelled_HTTP_token_reaches_the_exchange()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var exchange = new Fake((_, ct) => { Assert.Equal(cts.Token, ct); ct.ThrowIfCancellationRequested(); throw new Exception(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BrokerChatV2Persistence(exchange).ResolveAsync(Login(), cts.Token).AsTask());
        Assert.Equal(1, exchange.Calls);
    }
    [Fact]
    public async Task A_response_from_a_different_request_cannot_be_consumed()
    {
        var exchange = new Fake((_, _) => ValueTask.FromResult(new BrokerResponse(1, Guid.NewGuid(), ChatV2Failure.Resource, null)));
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => new BrokerChatV2Persistence(exchange).ResolveAsync(Login(), default).AsTask());
        Assert.Equal(ChatV2Failure.Contract, error.Failure);
    }
    [Fact]
    public async Task A_missing_typed_result_cannot_be_treated_as_an_empty_success()
    {
        var exchange = new Fake((request, _) => ValueTask.FromResult(new BrokerResponse(1, request.Correlation, null, null)));
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => new BrokerChatV2Persistence(exchange).OpenPrivateAsync(Login(), 8, default).AsTask());
        Assert.Equal(ChatV2Failure.Contract, error.Failure);
    }
    private sealed class Fake(Func<BrokerRequest, CancellationToken, ValueTask<BrokerResponse>> execute) : IChatV2BrokerExchange
    {
        public int Calls; public BrokerRequest? Last;
        public ValueTask<BrokerResponse> ExchangeAsync(BrokerRequest request, CancellationToken ct)
        { Calls++; Last = request; return execute(request, ct); }
    }
}
