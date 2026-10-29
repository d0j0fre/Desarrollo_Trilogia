using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_Final.Controllers;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class RouteDispatchWorkflowTests
{
    [Theory]
    [InlineData(nameof(RoutesAdminController.NotifyEmail))]
    [InlineData(nameof(RoutesAdminController.EmailReceipt))]
    public void Mail_actions_require_post_csrf_rate_limit_and_closed_selectors(string name)
    {
        var method = typeof(RoutesAdminController).GetMethod(name)!;
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal("route-dispatch-notification", method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(new[] { typeof(int), typeof(Guid), typeof(long), typeof(Guid) }, method.GetParameters().Select(x => x.ParameterType));
        Assert.Equal(new[] { "rutaId", "dispatchId", "eventId", "eventGuid" }, method.GetParameters().Select(x => x.Name));
        var authorization = typeof(RoutesAdminController).GetCustomAttribute<AdminAuthorizeAttribute>()!;
        Assert.Equal("RUTAS_GESTIONAR", authorization.Arguments![1]);
        Assert.False((bool)authorization.Arguments![2]);
    }

    [Fact]
    public async Task Lost_commit_ack_preserves_the_exact_dispatch_result()
    {
        var dispatch = Guid.NewGuid();
        var result = new RouteDispatchResultViewModel(10, dispatch, [Item(dispatch, 1)]);
        var cause = new IOException("Synthetic lost acknowledgement.");

        var error = await Assert.ThrowsAsync<RouteDispatchCommitUnconfirmedException>(() =>
            OriginalLoginTransaction.CommitResultAsync(result, () => Task.FromException(cause),
                (observed, failure) => new RouteDispatchCommitUnconfirmedException(observed, failure)));

        Assert.Same(result, error.Result);
        Assert.Same(cause, error.InnerException);
        Assert.Equal(dispatch, error.Result.DispatchId);
    }

    [Fact]
    public async Task Batch_has_at_most_two_inflight_and_attempts_each_eligible_event_once()
    {
        var dispatch = Guid.NewGuid();
        var items = Enumerable.Range(1, 5).Select(id => Item(dispatch, id)).ToArray();
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new System.Collections.Concurrent.ConcurrentDictionary<long, int>();
        int active = 0, maximum = 0;
        var batch = RouteDispatchNotificationBatch.SendOnceAsync(items, async (item, ct) =>
        {
            calls.AddOrUpdate(item.EventId, 1, (_, count) => count + 1);
            int now = Interlocked.Increment(ref active);
            int old;
            do { old = maximum; if (old >= now) break; } while (Interlocked.CompareExchange(ref maximum, now, old) != old);
            if (now == 2) bothStarted.TrySetResult();
            await release.Task.WaitAsync(ct);
            Interlocked.Decrement(ref active);
            return item.EventId == 3 ? "Unknown" : "SmtpAccepted";
        }, default);

        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, calls.Count);
        release.SetResult();
        var result = await batch;

        Assert.Equal(2, maximum);
        Assert.Equal(5, calls.Count);
        Assert.All(calls.Values, count => Assert.Equal(1, count));
        Assert.Equal(new RouteDispatchNotificationBatchResult(5, 4, 1), result);
    }

    [Fact]
    public async Task Unknown_transport_result_is_not_replayed_and_ineligible_recipient_is_skipped()
    {
        var dispatch = Guid.NewGuid();
        int calls = 0;
        var result = await RouteDispatchNotificationBatch.SendOnceAsync(
            [Item(dispatch, 1), Item(dispatch, 2) with { NotifyEligible = false }], (_, _) =>
            {
                calls++;
                throw new HttpRequestException("Synthetic lost response.");
            }, default);

        Assert.Equal(1, calls);
        Assert.Equal(new RouteDispatchNotificationBatchResult(1, 0, 1), result);
    }

    [Fact]
    public async Task Already_cancelled_request_does_not_start_sends()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        int calls = 0;
        var result = await RouteDispatchNotificationBatch.SendOnceAsync([Item(Guid.NewGuid(), 1)], (_, _) =>
        {
            calls++;
            return Task.FromResult("SmtpAccepted");
        }, cancellation.Token);

        Assert.Equal(0, calls);
        Assert.Equal(new RouteDispatchNotificationBatchResult(1, 0, 1), result);
    }

    [Fact]
    public void Sql_candidate_keeps_dispatch_atomic_and_recovers_only_the_owned_request()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "database"))) root = root.Parent;
        var sql = File.ReadAllText(Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "database", "migrations", "0059_stage2_route_dispatch_notifications.sql"));

        Assert.Contains("INSERT @Recipients EXEC dbo.sp_Rutas_Dispatch", sql, StringComparison.Ordinal);
        Assert.Contains("OriginalLoginId=@LoginId AND r.Actor=@UsuarioId", sql, StringComparison.Ordinal);
        Assert.Contains("IF @OperationId=''00000000-0000-0000-0000-000000000000''", sql, StringComparison.Ordinal);
        Assert.Contains("SELECT @OperationId=r.OperationId", sql, StringComparison.Ordinal);
        Assert.Contains("IF @Operation=N''Dispatch'' THROW 59504", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("GRANT ", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static RouteDispatchNotificationItemViewModel Item(Guid dispatch, int id) => new(dispatch, id, Guid.NewGuid(), id, id, true);
}
