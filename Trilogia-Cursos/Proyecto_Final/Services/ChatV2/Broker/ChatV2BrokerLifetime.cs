namespace Proyecto_Final.Services.ChatV2.Broker;

/// <summary>One deadline includes queueing and work. A slot remains owned until the operation actually exits.</summary>
internal sealed class ChatV2BrokerLifetime : IDisposable
{
    internal static readonly TimeSpan OperationBudget = TimeSpan.FromSeconds(15);
    private readonly SemaphoreSlim _slots;
    private readonly int _capacity;
    private int _admitted, _active;
    internal int Admitted => Volatile.Read(ref _admitted);
    internal int Active => Volatile.Read(ref _active);
    internal ChatV2BrokerLifetime(int parallel = 4, int queued = 8)
    {
        if (parallel is < 1 or > 16 || queued is < 0 or > 32) throw new ArgumentOutOfRangeException(nameof(parallel));
        _slots = new(parallel, parallel); _capacity = parallel + queued;
    }
    internal async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task> peerClosed, CancellationToken stop, TimeSpan? testBudget = null)
    {
        if (Interlocked.Increment(ref _admitted) > _capacity)
        { Interlocked.Decrement(ref _admitted); throw new ChatV2Exception(ChatV2Failure.Dependency); }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
        deadline.CancelAfter(testBudget ?? OperationBudget);
        using var watching = new CancellationTokenSource();
        bool ownsSlot = false;
        Task watch = ObservePeer();
        try
        {
            await _slots.WaitAsync(deadline.Token); ownsSlot = true; Interlocked.Increment(ref _active);
            deadline.Token.ThrowIfCancellationRequested();
            var result = await operation(deadline.Token);
            // A late dependency result cannot become a successful response after disconnect/deadline.
            deadline.Token.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            watching.Cancel();
            try { await watch; }
            finally
            {
                if (ownsSlot) { Interlocked.Decrement(ref _active); _slots.Release(); }
                Interlocked.Decrement(ref _admitted);
            }
        }
        async Task ObservePeer()
        {
            try { await peerClosed(watching.Token); deadline.Cancel(); }
            catch (OperationCanceledException) when (watching.IsCancellationRequested) { }
            catch { deadline.Cancel(); } // Transport faults cancel work; never detach a watcher.
        }
    }
    public void Dispose()
    {
        if (Admitted != 0) throw new InvalidOperationException("Drain broker operations before disposing.");
        _slots.Dispose();
    }
}
