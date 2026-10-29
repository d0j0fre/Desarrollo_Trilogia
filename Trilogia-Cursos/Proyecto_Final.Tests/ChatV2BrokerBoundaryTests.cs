using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Proyecto_Final.Services;
using Proyecto_Final.Services.ChatV2;
using Proyecto_Final.Services.ChatV2.Broker;

namespace Proyecto_Final.Tests;

public sealed class ChatV2BrokerBoundaryTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task UntilCancelled(CancellationToken ct) => await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    private static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 7, 2, Guid.NewGuid(), issued, issued.AddHours(8), new byte[32]);
    }
    private static BrokerRequest Request(BrokerOperation operation = BrokerOperation.Resolve)
        => new(1, Guid.NewGuid(), operation, BrokerLogin.Capture(Login()));

    [Fact]
    public void Peer_cancellation_is_not_misclassified_when_only_the_inner_lifetime_token_was_cancelled()
    {
        Assert.Equal("cancelled-after-work-exit", BrokerObservation.FailurePhase(new TaskCanceledException(), false));
        Assert.Equal("cancelled-after-work-exit", BrokerObservation.FailurePhase(new IOException(), true));
        Assert.Equal("request-failed", BrokerObservation.FailurePhase(new IOException(), false));
    }

    [Fact]
    public async Task Frame_roundtrip_preserves_datetime2_ticks_and_binding_without_log_representation()
    {
        var request = Request(); using var stream = new MemoryStream();
        await BrokerFrame.WriteAsync(stream, request, BrokerFrame.RequestLimit, default); stream.Position = 0;
        var restored = await BrokerFrame.ReadAsync<BrokerRequest>(stream, BrokerFrame.RequestLimit, default);
        Assert.Equal(request.Login.ExpiresUtc.Ticks, restored.Login.Restore().ExpiresUtc.Ticks);
        Assert.Equal(request.Login.BindingHash, restored.Login.BindingHash);
        Assert.Equal(nameof(BrokerRequest), request.ToString());
        Assert.Equal(nameof(BrokerLogin), request.Login.ToString());
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(BrokerFrame.RequestLimit + 1)]
    public async Task Invalid_length_is_rejected_before_body_allocation(int length)
    {
        byte[] prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, length);
        using var stream = new MemoryStream(prefix);
        await Assert.ThrowsAsync<InvalidDataException>(() => BrokerFrame.ReadAsync<BrokerRequest>(stream,
            BrokerFrame.RequestLimit, default).AsTask());
    }
    [Fact]
    public async Task Unknown_fields_and_truncation_are_closed()
    {
        var request = Request();
        var json = JsonSerializer.Serialize(request, BrokerFrame.Json).TrimEnd('}') + ",\"Sql\":\"arbitrary\"}";
        byte[] body = Encoding.UTF8.GetBytes(json), prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, body.Length);
        using var stream = new MemoryStream(prefix.Concat(body).ToArray());
        await Assert.ThrowsAsync<JsonException>(() => BrokerFrame.ReadAsync<BrokerRequest>(stream, BrokerFrame.RequestLimit, default).AsTask());
        using var shortFrame = new MemoryStream(prefix.Concat(body.Take(5)).ToArray());
        await Assert.ThrowsAsync<EndOfStreamException>(() => BrokerFrame.ReadAsync<BrokerRequest>(shortFrame, BrokerFrame.RequestLimit, default).AsTask());
    }
    [Fact]
    public async Task Cancelled_frames_do_not_wait_for_a_body()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        using var stream = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BrokerFrame.ReadAsync<BrokerRequest>(stream,
            BrokerFrame.RequestLimit, cts.Token).AsTask());
    }
    [Fact]
    public async Task Queue_has_a_fixed_bound_and_releases_both_active_and_waiting_slots()
    {
        using var lifetime = new ChatV2BrokerLifetime(1, 1); using var stop = new CancellationTokenSource();
        var entered = Signal();
        var first = lifetime.ExecuteAsync(async ct => { entered.SetResult(); await UntilCancelled(ct); return true; }, UntilCancelled, stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = lifetime.ExecuteAsync(_ => Task.FromResult(true), UntilCancelled, stop.Token);
        Assert.Equal(2, lifetime.Admitted); Assert.Equal(1, lifetime.Active);
        var denied = await Assert.ThrowsAsync<ChatV2Exception>(() => lifetime.ExecuteAsync(_ => Task.FromResult(true), UntilCancelled, default));
        Assert.Equal(ChatV2Failure.Dependency, denied.Failure);
        stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.Equal(0, lifetime.Admitted); Assert.Equal(0, lifetime.Active);
    }
    [Fact]
    public async Task Peer_disconnect_reaches_work_and_the_watcher_is_joined()
    {
        using var lifetime = new ChatV2BrokerLifetime(1, 0);
        var entered = Signal(); var closed = Signal(); bool watcherExited = false, workExited = false;
        var run = lifetime.ExecuteAsync(async ct =>
        {
            entered.SetResult(); try { await UntilCancelled(ct); return 1; } finally { workExited = true; }
        }, async ct => { try { await closed.Task.WaitAsync(ct); } finally { watcherExited = true; } }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); closed.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(workExited); Assert.True(watcherExited); Assert.Equal(0, lifetime.Admitted);
    }
    [Fact]
    public async Task Completion_cancels_and_joins_the_disconnect_watcher()
    {
        using var lifetime = new ChatV2BrokerLifetime(); bool exited = false;
        int result = await lifetime.ExecuteAsync(_ => Task.FromResult(5), async ct =>
        { try { await UntilCancelled(ct); } finally { exited = true; } }, default);
        Assert.Equal(5, result); Assert.True(exited); Assert.Equal(0, lifetime.Active);
    }
    [Fact]
    public async Task A_noncooperative_dependency_keeps_its_slot_until_actual_exit_and_cannot_report_success()
    {
        using var lifetime = new ChatV2BrokerLifetime(1, 0); using var stop = new CancellationTokenSource();
        var entered = Signal(); var finish = Signal();
        var run = lifetime.ExecuteAsync(async _ => { entered.SetResult(); await finish.Task; return 1; }, UntilCancelled, stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); stop.Cancel();
        Assert.False(run.IsCompleted); Assert.Equal(1, lifetime.Active);
        await Assert.ThrowsAsync<ChatV2Exception>(() => lifetime.ExecuteAsync(_ => Task.FromResult(2), UntilCancelled, default));
        finish.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(0, lifetime.Active);
    }
    [Fact]
    public async Task Dependency_failure_is_propagated_after_watcher_cleanup()
    {
        using var lifetime = new ChatV2BrokerLifetime(); bool joined = false;
        await Assert.ThrowsAsync<IOException>(() => lifetime.ExecuteAsync<int>(_ => throw new IOException(), async ct =>
        { try { await UntilCancelled(ct); } finally { joined = true; } }, default));
        Assert.True(joined); Assert.Equal(0, lifetime.Admitted);
    }
    [Fact]
    public async Task Expired_and_malformed_logins_never_reach_the_persistence_port()
    {
        var persistence = new Fake(); var dispatch = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        var request = Request();
        foreach (var login in new[] { request.Login with { BindingHash = new byte[1] },
            request.Login with { IssuedUtc = DateTime.UtcNow.AddHours(-9), ExpiresUtc = DateTime.UtcNow.AddHours(-1) } })
        {
            var result = await dispatch.DispatchAsync(request with { Login = login }, default);
            Assert.Equal(ChatV2Failure.Session, result.Failure);
        }
        Assert.Equal(0, persistence.Resolutions);
    }
    [Fact]
    public async Task A_changed_actor_from_resolution_is_rejected_without_executing_work()
    {
        var persistence = new Fake { ChangeActor = true }; var dispatch = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        var response = await dispatch.DispatchAsync(Request(BrokerOperation.Send), default);
        Assert.Equal(ChatV2Failure.Session, response.Failure); Assert.Equal(0, persistence.Executions);
    }
    [Theory]
    [InlineData((int)BrokerOperation.Send)] [InlineData((int)BrokerOperation.Receipt)] [InlineData((int)BrokerOperation.Prepare)]
    public async Task Captured_epochs_and_payload_cannot_be_replaced(int value)
    {
        var operation = (BrokerOperation)value;
        var persistence = new Fake(); var dispatch = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        var request = Request(operation) with { Resource = new(ChatV2Domain.Private, 1), RequestId = Guid.NewGuid(),
            ContentHash = ChatV2Continuity.ContentHash("original"), ActorEpoch = 8, ResourceEpoch = 1,
            IntentExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5), Content = "changed" };
        Assert.Equal(ChatV2Failure.Conflict, (await dispatch.DispatchAsync(request, default)).Failure);
        request = request with { ActorEpoch = 7 };
        if (operation != BrokerOperation.Prepare)
            Assert.Equal(ChatV2Failure.Conflict, (await dispatch.DispatchAsync(request, default)).Failure);
        Assert.Equal(0, persistence.Executions);
    }
    [Fact]
    public async Task Receipt_preserves_original_UUID_and_never_carries_a_body()
    {
        var persistence = new Fake(); var dispatch = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        var request = Request(BrokerOperation.Receipt) with { Resource = new(ChatV2Domain.Private, 1), RequestId = Guid.NewGuid(),
            ContentHash = ChatV2Continuity.ContentHash("original"), ActorEpoch = 7, ResourceEpoch = 3,
            IntentExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) };
        var result = await dispatch.DispatchAsync(request, default);
        Assert.Null(result.Failure); Assert.Equal(1, persistence.Executions);
        Assert.Equal(request.RequestId, persistence.RequestId); Assert.True(persistence.ReceiptOnly); Assert.Null(persistence.Content);
    }
    [Fact]
    public async Task Unknown_protocol_versions_do_not_reach_persistence()
    {
        var persistence = new Fake(); var dispatch = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        await Assert.ThrowsAsync<ChatV2Exception>(() => dispatch.DispatchAsync(Request() with { Version = 2 }, default));
        Assert.Equal(0, persistence.Resolutions);
    }
    [Theory]
    [InlineData(ChatV2Domain.Department, true, false, ChatV2Kind.Internal, ChatV2Capabilities.InternalUse, true)]
    [InlineData(ChatV2Domain.Department, false, true, ChatV2Kind.Internal, ChatV2Capabilities.InternalUse, false)]
    [InlineData(ChatV2Domain.Department, true, true, ChatV2Kind.Client, ChatV2Capabilities.CustomerUse, false)]
    [InlineData(ChatV2Domain.Customer, true, true, ChatV2Kind.Client, ChatV2Capabilities.CustomerUse, true)]
    [InlineData(ChatV2Domain.Customer, true, true, ChatV2Kind.Internal, ChatV2Capabilities.AttendCustomers, true)]
    [InlineData(ChatV2Domain.Customer, true, false, ChatV2Kind.Client, ChatV2Capabilities.CustomerUse, false)]
    [InlineData(ChatV2Domain.Customer, true, true, ChatV2Kind.Internal, ChatV2Capabilities.ManageAttention, false)]
    [InlineData(ChatV2Domain.Private, true, true, ChatV2Kind.Client, ChatV2Capabilities.CustomerUse, false)]
    public async Task Resource_domains_require_a_composed_provider_and_exact_actor_capability(ChatV2Domain domain,
        bool departments, bool customers, ChatV2Kind kind, ChatV2Capabilities capabilities, bool allowed)
    {
        var persistence = new Fake { SupportsDepartment = departments, SupportsCustomer = customers, Kind = kind, Capabilities = capabilities };
        var dispatch = new ChatV2BrokerDispatcher(persistence, TimeProvider.System);
        var request = Request(BrokerOperation.Prepare) with { Resource = new(domain, 1), RequestId = Guid.NewGuid(),
            ContentHash = ChatV2Continuity.ContentHash("original"), ActorEpoch = 7 };
        var result = await dispatch.DispatchAsync(request, default);
        if (allowed) Assert.Null(result.Failure); else Assert.Equal(ChatV2Failure.Forbidden, result.Failure);
        Assert.Equal(allowed ? 1 : 0, persistence.Preparations);
    }
    private sealed class Fake : IChatV2IntentPersistence
    {
        public int Resolutions, Executions, Preparations; public bool ChangeActor, ReceiptOnly; public Guid RequestId; public string? Content;
        public bool SupportsDepartment { get; set; }
        public bool SupportsCustomer { get; set; }
        public ChatV2Kind Kind = ChatV2Kind.Internal;
        public ChatV2Capabilities Capabilities = ChatV2Capabilities.InternalUse;
        public ValueTask<ChatV2LiveIdentity> ResolveAsync(OriginalLoginContext login, CancellationToken ct)
        {
            Resolutions++; return ValueTask.FromResult(new ChatV2LiveIdentity(login.LoginId, login.LoginId,
                login.UserId + (ChangeActor ? 1 : 0), login.ProfileId, Kind, login.SecurityStamp,
                7, new(login.ExpiresUtc), Capabilities));
        }
        public ValueTask<ChatV2IntentEpochs> PrepareAsync(OriginalLoginContext l, ChatV2LiveIdentity i,
            ChatV2Resource r, Guid u, string h, CancellationToken ct) { Preparations++; return ValueTask.FromResult(new ChatV2IntentEpochs(7, 3, DateTimeOffset.UtcNow.AddMinutes(5))); }
        public ValueTask<ChatV2Receipt> ExecuteAsync(ChatV2BoundIntent intent, string? body, bool receiptOnly, CancellationToken ct)
        {
            Executions++; RequestId = intent.Request; ReceiptOnly = receiptOnly; Content = body;
            return ValueTask.FromResult(new ChatV2Receipt(intent.Request, intent.Resource.Domain, intent.Resource.Id,
                1, 1, DateTime.UtcNow, "PERSISTED", "original", intent.ResourceEpoch));
        }
    }
}
