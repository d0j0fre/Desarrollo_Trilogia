using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace Proyecto_Final.Services.ChatV2.Broker;

/// <summary>Owns every host task, including factories that fail before their first await.</summary>
internal static class ChatV2BrokerHostLifetime
{
    internal static async Task RunAsync(Func<CancellationToken, Task> startServing,
        Func<CancellationToken, Task> startWatching, Func<Task> startWriting,
        ChannelWriter<BrokerObservation> events, CancellationTokenSource shutdown, Action? started = null)
    {
        ExceptionDispatchInfo? primary = null;
        int completingWriter = 0;
        // Starting a failed factory never skips the other task owners or the cleanup below.
        Task writing = Supervise(startWriting, "diagnostics", writer: true);
        Task serving = Supervise(() => startServing(shutdown.Token), "serving");
        Task watching = Supervise(() => startWatching(shutdown.Token), "control");
        try
        {
            if (!shutdown.IsCancellationRequested) started?.Invoke();
            await Task.WhenAny(serving, watching, writing);
        }
        catch (Exception error) { Capture(error); }
        finally
        {
            RequestStop();
            // Cancellation does not prove that a dependency has exited. Drain the actual handlers first.
            try { await Task.WhenAll(serving, watching); }
            catch { /* Both tasks were observed; their original failure was captured by Supervise. */ }
            Volatile.Write(ref completingWriter, 1);
            events.TryComplete();
            try { await writing; }
            catch { /* Observe writer failures even when serving or control already failed. */ }
        }
        primary?.Throw();

        async Task Supervise(Func<Task> start, string role, bool writer = false)
        {
            try
            {
                await start();
                if (writer ? Volatile.Read(ref completingWriter) == 0 : !shutdown.IsCancellationRequested)
                    throw new InvalidOperationException("Broker " + role + " ended before shutdown.");
            }
            catch (Exception error)
            {
                // A requested shutdown may cancel a worker normally. A later cleanup failure must not
                // replace the exception that caused shutdown in the first place.
                if (error is not OperationCanceledException || !shutdown.IsCancellationRequested) Capture(error);
                RequestStop();
                throw;
            }
        }
        void Capture(Exception error) => Interlocked.CompareExchange(ref primary, ExceptionDispatchInfo.Capture(error), null);
        void RequestStop()
        {
            try { shutdown.Cancel(); }
            catch (Exception error) { Capture(error); }
        }
    }
}
