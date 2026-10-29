using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

internal sealed record RouteDispatchNotificationBatchResult(int Eligible, int Accepted, int Unconfirmed);

internal static class RouteDispatchNotificationBatch
{
    // Each item is attempted at most once. A route with many stops has a single
    // bounded budget; remaining items are continued manually through receipts.
    internal static async Task<RouteDispatchNotificationBatchResult> SendOnceAsync(
        IReadOnlyList<RouteDispatchNotificationItemViewModel> items,
        Func<RouteDispatchNotificationItemViewModel, CancellationToken, Task<string>> send,
        CancellationToken cancellationToken)
    {
        var eligible = items.Where(x => x.NotifyEligible).ToArray();
        int accepted = 0;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(18));
        try
        {
            await Parallel.ForEachAsync(eligible,
                new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = budget.Token },
                async (item, token) =>
                {
                    try
                    {
                        if (await send(item, token) == "SmtpAccepted") Interlocked.Increment(ref accepted);
                    }
                    catch (Exception error) when (error is HttpRequestException or OperationCanceledException
                        or RouteDispatchNotificationTransportException or UnauthorizedAccessException)
                    {
                        // The durable receipt decides what can be continued later.
                        // A timeout/unknown response never causes another Send here.
                    }
                });
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested) { }
        return new(eligible.Length, accepted, eligible.Length - accepted);
    }
}
