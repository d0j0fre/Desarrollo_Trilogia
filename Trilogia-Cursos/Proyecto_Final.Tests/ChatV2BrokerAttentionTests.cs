using System.Text.Json;
using Proyecto_Final.Services;
using Proyecto_Final.Services.ChatV2;
using Proyecto_Final.Services.ChatV2.Broker;

namespace Proyecto_Final.Tests;

public sealed class ChatV2BrokerAttentionTests
{
    private static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 7, 2, Guid.NewGuid(), issued, issued.AddHours(8), new byte[32]);
    }
    private static BrokerRequest Request(OriginalLoginContext login, BrokerOperation operation)
        => new(1, Guid.NewGuid(), operation, BrokerLogin.Capture(login));

    [Fact]
    public async Task Queue_crosses_the_broker_as_metadata_with_the_original_login()
    {
        var login = Login(); var persistence = new Persistence(login);
        var exchange = new Loopback(new(persistence, TimeProvider.System));
        var rows = await new BrokerChatV2Persistence(exchange).QueueAsync(login, default);
        Assert.Equal(login.LoginId, persistence.LastLogin!.LoginId);
        Assert.Equal(new ChatV2QueueItem(11, true, 3, "OPEN"), Assert.Single(rows));
        Assert.Equal(1, persistence.ResolveCalls); Assert.Equal(1, persistence.QueueCalls);
        Assert.Equal(BrokerOperation.Queue, exchange.Last!.Operation);
        Assert.Null(exchange.Last.Content); Assert.Null(exchange.Last.Resource); Assert.Null(exchange.Last.AttentionTransfer);
        Assert.All(exchange.Last.Login.BindingHash, b => Assert.Equal(0, b));
        var json = JsonSerializer.Serialize(rows);
        Assert.DoesNotContain("Content", json); Assert.DoesNotContain("Sender", json); Assert.DoesNotContain("Client", json);
    }

    [Fact]
    public async Task Transfer_preserves_typed_assignment_CAS_and_does_not_grant_membership()
    {
        var login = Login(); var persistence = new Persistence(login);
        var exchange = new Loopback(new(persistence, TimeProvider.System));
        Assert.Equal(4, await new BrokerChatV2Persistence(exchange).TransferAsync(login, 11, 8, 3, default));
        Assert.Equal(new BrokerAttentionTransfer(11, 8, 3), exchange.Last!.AttentionTransfer);
        Assert.Equal((11, 8, 3L), persistence.LastTransfer);
        Assert.Equal(login.LoginId, persistence.LastLogin!.LoginId);
        Assert.Equal(1, persistence.TransferCalls); Assert.Equal(0, persistence.QueueCalls);
        Assert.Null(exchange.Last.Resource); Assert.Null(exchange.Last.RequestId); Assert.Null(exchange.Last.Content);
    }

    [Theory]
    [InlineData(ChatV2Kind.Client, ChatV2Capabilities.ManageAttention)]
    [InlineData(ChatV2Kind.Internal, ChatV2Capabilities.InternalUse)]
    [InlineData(ChatV2Kind.Internal, ChatV2Capabilities.ManageDepartments)]
    [InlineData(ChatV2Kind.Internal, ChatV2Capabilities.AttendCustomers)]
    public async Task Canonical_kind_and_attention_capability_are_required(ChatV2Kind kind, ChatV2Capabilities capabilities)
    {
        var login = Login(); var persistence = new Persistence(login) { Kind = kind, Capabilities = capabilities };
        var dispatcher = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        foreach (var operation in new[] { BrokerOperation.Queue, BrokerOperation.Transfer })
        {
            var result = await dispatcher.DispatchAsync(Request(login, operation) with { AttentionTransfer = operation == BrokerOperation.Transfer ? new(11, 8, 3) : null }, default);
            Assert.Equal(ChatV2Failure.Forbidden, result.Failure); Assert.Null(result.Result);
        }
        Assert.Equal(0, persistence.QueueCalls); Assert.Equal(0, persistence.TransferCalls);
    }

    [Theory]
    [InlineData(0, 8, 3L)]
    [InlineData(11, 0, 3L)]
    [InlineData(11, 8, -1L)]
    [InlineData(11, 8, long.MaxValue)]
    public async Task Invalid_assignment_inputs_are_rejected_before_mutation(int caseId, int agent, long version)
    {
        var login = Login(); var persistence = new Persistence(login);
        var result = await new ChatV2BrokerDispatcher(persistence, TimeProvider.System).DispatchAsync(
            Request(login, BrokerOperation.Transfer) with { AttentionTransfer = new(caseId, agent, version) }, default);
        Assert.Equal(ChatV2Failure.Validation, result.Failure); Assert.Equal(0, persistence.TransferCalls);
    }

    [Fact]
    public async Task An_attention_manager_cannot_assign_a_case_to_themselves()
    {
        var login = Login(); var persistence = new Persistence(login);
        var result = await new ChatV2BrokerDispatcher(persistence, TimeProvider.System).DispatchAsync(
            Request(login, BrokerOperation.Transfer) with { AttentionTransfer = new(11, login.UserId, 3) }, default);
        Assert.Equal(ChatV2Failure.Forbidden, result.Failure); Assert.Equal(0, persistence.TransferCalls);
    }

    [Fact]
    public async Task Management_frames_cannot_carry_message_content_or_resource_access()
    {
        var login = Login(); var persistence = new Persistence(login);
        var dispatcher = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        var invalid = new[]
        {
            Request(login, BrokerOperation.Resolve) with { AttentionTransfer = new(11, 8, 3) },
            Request(login, BrokerOperation.Queue) with { Content = "private" },
            Request(login, BrokerOperation.Queue) with { Resource = new(ChatV2Domain.Customer, 11) },
            Request(login, BrokerOperation.Queue) with { AttentionTransfer = new(11, 8, 3) },
            Request(login, BrokerOperation.Transfer),
            Request(login, BrokerOperation.Transfer) with { AttentionTransfer = new(11, 8, 3), Cursor = "cursor" },
            Request(login, BrokerOperation.History) with { Resource = new(ChatV2Domain.Private, 11), AttentionTransfer = new(11, 8, 3) }
        };
        foreach (var request in invalid)
            Assert.Equal(ChatV2Failure.Validation, (await dispatcher.DispatchAsync(request, default)).Failure);
        Assert.Equal(0, persistence.QueueCalls); Assert.Equal(0, persistence.TransferCalls);
    }

    [Fact]
    public async Task Every_management_call_resolves_current_authority_and_preserves_revocation_denial()
    {
        var login = Login(); var persistence = new Persistence(login);
        var dispatcher = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        Assert.Null((await dispatcher.DispatchAsync(Request(login, BrokerOperation.Queue), default)).Failure);
        persistence.Revoked = true;
        var result = await dispatcher.DispatchAsync(Request(login, BrokerOperation.Queue), default);
        Assert.Equal(ChatV2Failure.Session, result.Failure); Assert.Null(result.Result);
        Assert.Equal(2, persistence.ResolveCalls); Assert.Equal(1, persistence.QueueCalls);
    }

    [Fact]
    public async Task A_different_original_session_cannot_reuse_another_identity()
    {
        var login = Login(); var persistence = new Persistence(login) { DifferentSession = true };
        var result = await new ChatV2BrokerDispatcher(persistence, TimeProvider.System).DispatchAsync(Request(login, BrokerOperation.Queue), default);
        Assert.Equal(ChatV2Failure.Session, result.Failure); Assert.Equal(0, persistence.QueueCalls);
    }

    [Fact]
    public async Task Unsupported_backend_remains_a_dependency_failure()
    {
        var login = Login(); var unsupported = new Unsupported(new Persistence(login));
        var result = await new ChatV2BrokerDispatcher(unsupported, TimeProvider.System).DispatchAsync(Request(login, BrokerOperation.Queue), default);
        Assert.Equal(ChatV2Failure.Dependency, result.Failure); Assert.Null(result.Result);
    }

    [Fact]
    public async Task Transfer_response_must_confirm_exactly_the_next_assignment_version()
    {
        var login = Login(); var persistence = new Persistence(login) { TransferResult = 3 };
        var exchange = new Loopback(new(persistence, TimeProvider.System));
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => new BrokerChatV2Persistence(exchange).TransferAsync(login, 11, 8, 3, default).AsTask());
        Assert.Equal(ChatV2Failure.Contract, error.Failure); Assert.Equal(1, persistence.TransferCalls);
    }

    [Fact]
    public async Task Cancellation_reaches_the_management_persistence()
    {
        var login = Login(); var persistence = new Persistence(login);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ChatV2BrokerDispatcher(persistence, TimeProvider.System)
            .DispatchAsync(Request(login, BrokerOperation.Queue), cts.Token));
        Assert.Equal(cts.Token, persistence.LastToken); Assert.Equal(0, persistence.QueueCalls);
    }

    [Fact]
    public void Personal_operation_ordinals_are_preserved()
    {
        Assert.Equal(0, (int)BrokerOperation.Resolve); Assert.Equal(9, (int)BrokerOperation.Leave);
        Assert.Equal(10, (int)BrokerOperation.Queue); Assert.Equal(11, (int)BrokerOperation.Transfer);
    }

    [Fact]
    public void Personal_frames_do_not_add_a_null_field_rejected_by_an_older_broker()
    {
        var json = JsonSerializer.Serialize(Request(Login(), BrokerOperation.Resolve), BrokerFrame.Json);
        Assert.DoesNotContain("AttentionTransfer", json);
    }

    private sealed class Loopback(ChatV2BrokerDispatcher dispatcher) : IChatV2BrokerExchange
    {
        public BrokerRequest? Last;
        public async ValueTask<BrokerResponse> ExchangeAsync(BrokerRequest request, CancellationToken ct)
        { Last = request; return await dispatcher.DispatchAsync(request, ct); }
    }
    private sealed class Persistence(OriginalLoginContext original) : IChatV2IntentPersistence
    {
        public ChatV2Kind Kind = ChatV2Kind.Internal;
        public ChatV2Capabilities Capabilities = ChatV2Capabilities.ManageAttention;
        public bool Revoked, DifferentSession;
        public int ResolveCalls, QueueCalls, TransferCalls;
        public long TransferResult = 4;
        public CancellationToken LastToken;
        public OriginalLoginContext? LastLogin;
        public (int, int, long) LastTransfer;
        public ValueTask<ChatV2LiveIdentity> ResolveAsync(OriginalLoginContext login, CancellationToken ct)
        {
            ResolveCalls++; LastToken = ct; ct.ThrowIfCancellationRequested();
            if (Revoked) throw new ChatV2Exception(ChatV2Failure.Session);
            return ValueTask.FromResult(new ChatV2LiveIdentity(original.LoginId, DifferentSession ? Guid.NewGuid() : original.LoginId,
                original.UserId, original.ProfileId, Kind, original.SecurityStamp, 3, new(original.ExpiresUtc), Capabilities));
        }
        public ValueTask<IReadOnlyList<ChatV2QueueItem>> QueueAsync(OriginalLoginContext login, CancellationToken ct)
        { QueueCalls++; LastLogin = login; return ValueTask.FromResult<IReadOnlyList<ChatV2QueueItem>>([new(11, true, 3, "OPEN")]); }
        public ValueTask<long> TransferAsync(OriginalLoginContext login, int caseId, int newAgent, long expectedVersion, CancellationToken ct)
        { TransferCalls++; LastLogin = login; LastTransfer = (caseId, newAgent, expectedVersion); return ValueTask.FromResult(TransferResult); }
        public ValueTask<ChatV2IntentEpochs> PrepareAsync(OriginalLoginContext login, ChatV2LiveIdentity identity, ChatV2Resource resource, Guid request, string contentHash, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<ChatV2Receipt> ExecuteAsync(ChatV2BoundIntent intent, string? content, bool receiptOnly, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Unsupported(Persistence identity) : IChatV2IntentPersistence
    {
        public ValueTask<ChatV2LiveIdentity> ResolveAsync(OriginalLoginContext login, CancellationToken ct) => identity.ResolveAsync(login, ct);
        public ValueTask<ChatV2IntentEpochs> PrepareAsync(OriginalLoginContext login, ChatV2LiveIdentity current, ChatV2Resource resource, Guid request, string contentHash, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<ChatV2Receipt> ExecuteAsync(ChatV2BoundIntent intent, string? content, bool receiptOnly, CancellationToken ct) => throw new NotSupportedException();
    }
}
