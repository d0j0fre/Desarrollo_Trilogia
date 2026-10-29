using System.Threading.Channels;
using Proyecto_Final.Services.ChatV2.Broker;

namespace Proyecto_Final.Tests;

public sealed class ChatV2BrokerHostLifetimeTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task WaitForStop(CancellationToken ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct);

    [Fact]
    public async Task A_synchronous_serving_start_failure_still_joins_control_and_flushes_the_writer()
    {
        using var stop = new CancellationTokenSource(); var events = Channel.CreateUnbounded<BrokerObservation>();
        var original = new IOException("synthetic serving start failure"); bool watched = false, flushed = false, ready = false;
        async Task Watch(CancellationToken ct) { try { await WaitForStop(ct); } finally { watched = true; } }
        async Task Write() { await foreach (var _ in events.Reader.ReadAllAsync()) { } flushed = true; }
        var actual = await Assert.ThrowsAsync<IOException>(() => ChatV2BrokerHostLifetime.RunAsync(
            _ => throw original, Watch, Write, events.Writer, stop, () => ready = true).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Same(original, actual); Assert.True(watched); Assert.True(flushed); Assert.False(ready);
    }

    [Fact]
    public async Task The_original_serving_failure_survives_a_later_writer_flush_failure()
    {
        using var stop = new CancellationTokenSource(); var events = Channel.CreateUnbounded<BrokerObservation>();
        var original = new IOException("synthetic serving failure"); var flushError = new InvalidOperationException("synthetic flush failure");
        bool flushAttempted = false;
        async Task Write() { await foreach (var _ in events.Reader.ReadAllAsync()) { } flushAttempted = true; throw flushError; }
        var actual = await Assert.ThrowsAsync<IOException>(() => ChatV2BrokerHostLifetime.RunAsync(
            _ => Task.FromException(original), WaitForStop, Write, events.Writer, stop).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Same(original, actual); Assert.True(flushAttempted);
    }

    [Fact]
    public async Task A_synchronous_writer_start_failure_cancels_and_observes_both_other_workers()
    {
        using var stop = new CancellationTokenSource(); var events = Channel.CreateUnbounded<BrokerObservation>();
        var original = new IOException("synthetic diagnostics start failure"); bool served = false, watched = false;
        async Task Serve(CancellationToken ct) { try { await WaitForStop(ct); } finally { served = true; } }
        async Task Watch(CancellationToken ct) { try { await WaitForStop(ct); } finally { watched = true; } }
        var actual = await Assert.ThrowsAsync<IOException>(() => ChatV2BrokerHostLifetime.RunAsync(
            Serve, Watch, () => throw original, events.Writer, stop).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Same(original, actual); Assert.True(served); Assert.True(watched);
    }

    [Fact]
    public async Task A_control_failure_cancels_serving_and_drains_diagnostics()
    {
        using var stop = new CancellationTokenSource(); var events = Channel.CreateUnbounded<BrokerObservation>();
        var original = new IOException("synthetic control failure"); bool served = false, flushed = false;
        async Task Serve(CancellationToken ct) { try { await WaitForStop(ct); } finally { served = true; } }
        async Task Write() { await foreach (var _ in events.Reader.ReadAllAsync()) { } flushed = true; }
        var actual = await Assert.ThrowsAsync<IOException>(() => ChatV2BrokerHostLifetime.RunAsync(
            Serve, _ => Task.FromException(original), Write, events.Writer, stop).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Same(original, actual); Assert.True(served); Assert.True(flushed);
    }

    [Fact]
    public async Task Channel_completion_waits_for_the_actual_handler_cleanup()
    {
        using var stop = new CancellationTokenSource(); var events = Channel.CreateUnbounded<BrokerObservation>();
        var cleanupEntered = Signal(); var releaseCleanup = Signal(); bool flushed = false;
        async Task Serve(CancellationToken ct)
        {
            try { await WaitForStop(ct); }
            finally { cleanupEntered.SetResult(); await releaseCleanup.Task; }
        }
        async Task Write() { await foreach (var _ in events.Reader.ReadAllAsync()) { } flushed = true; }
        var run = ChatV2BrokerHostLifetime.RunAsync(Serve, WaitForStop, Write, events.Writer, stop);
        stop.Cancel(); await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(run.IsCompleted); Assert.False(events.Reader.Completion.IsCompleted); Assert.False(flushed);
        releaseCleanup.SetResult(); await run.WaitAsync(TimeSpan.FromSeconds(3)); Assert.True(flushed);
    }

    [Fact]
    public async Task A_readiness_callback_failure_is_preserved_after_all_workers_are_joined()
    {
        using var stop = new CancellationTokenSource(); var events = Channel.CreateUnbounded<BrokerObservation>();
        var original = new IOException("synthetic readiness failure"); bool served = false, watched = false, flushed = false;
        async Task Serve(CancellationToken ct) { try { await WaitForStop(ct); } finally { served = true; } }
        async Task Watch(CancellationToken ct) { try { await WaitForStop(ct); } finally { watched = true; } }
        async Task Write() { await foreach (var _ in events.Reader.ReadAllAsync()) { } flushed = true; }
        var actual = await Assert.ThrowsAsync<IOException>(() => ChatV2BrokerHostLifetime.RunAsync(
            Serve, Watch, Write, events.Writer, stop, () => throw original).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Same(original, actual); Assert.True(served); Assert.True(watched); Assert.True(flushed);
    }

    [Fact]
    public async Task Diagnostics_cannot_end_successfully_before_the_handlers_finish()
    {
        using var stop = new CancellationTokenSource(); var events = Channel.CreateUnbounded<BrokerObservation>();
        bool served = false, watched = false;
        async Task Serve(CancellationToken ct) { try { await WaitForStop(ct); } finally { served = true; } }
        async Task Watch(CancellationToken ct) { try { await WaitForStop(ct); } finally { watched = true; } }
        await Assert.ThrowsAsync<InvalidOperationException>(() => ChatV2BrokerHostLifetime.RunAsync(
            Serve, Watch, () => Task.CompletedTask, events.Writer, stop).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(served); Assert.True(watched);
    }
}
