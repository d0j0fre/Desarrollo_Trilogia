using System.Net.Http.Json;
using System.Text.Json;
using JJ.Commerce;

namespace Proyecto_Final.Services;

public interface IDeliveryNotificationClient
{
    Task<DeliveryNotificationReceipt> NotifyAsync(int routeOrder, Guid sync, CancellationToken ct);
    Task<DeliveryNotificationReceipt> ReceiptAsync(int routeOrder, Guid sync, Guid operation, CancellationToken ct);
}
public sealed class DeliveryNotificationTransportException(int status) : Exception("El estado del aviso de entrega no quedó confirmado.")
{ public int Status { get; } = status; }
public sealed class DisabledDeliveryNotificationClient : IDeliveryNotificationClient
{
    public Task<DeliveryNotificationReceipt> NotifyAsync(int routeOrder, Guid sync, CancellationToken ct) => throw new DeliveryNotificationTransportException(503);
    public Task<DeliveryNotificationReceipt> ReceiptAsync(int routeOrder, Guid sync, Guid operation, CancellationToken ct) => throw new DeliveryNotificationTransportException(503);
}

public sealed class DeliveryNotificationClient(HttpClient http, IHttpContextAccessor accessor, IConfiguration configuration) : IDeliveryNotificationClient
{
    public async Task<DeliveryNotificationReceipt> NotifyAsync(int routeOrder, Guid sync, CancellationToken ct)
    {
        var session = Session(routeOrder);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(18));
        var known = await Lookup(session, routeOrder, sync, Guid.Empty, deadline.Token);
        if (known.State is not ("None" or "Pending")) return known;
        var operation = known.OperationId;
        if (operation == Guid.Empty)
        {
            do { operation = Guid.NewGuid(); } while (operation == known.SyncGuid);
        }
        // Receipt is read-only. Send is issued ONCE; no HTTP timeout/error ever causes a replay.
        var result = await Call("send", new DeliveryNotificationCommand(session, routeOrder, known.SyncGuid, operation), operation, known.SyncGuid, false, deadline.Token);
        if (result.State == "AlreadyRequested")
        {
            // A concurrent request may have registered this original session's UUID.
            // Recover it without sending or adopting an attempt owned by another login.
            var own = await Lookup(session, routeOrder, known.SyncGuid, Guid.Empty, deadline.Token);
            if (own.State != "None") return own;
        }
        return result;
    }

    public Task<DeliveryNotificationReceipt> ReceiptAsync(int routeOrder, Guid sync, Guid operation, CancellationToken ct) =>
        Lookup(Session(routeOrder), routeOrder, sync, operation, ct);

    private CommerceSession Session(int routeOrder)
    {
        if (!configuration.GetValue<bool>("DeliveryNotifications:Enabled")) throw new DeliveryNotificationTransportException(503);
        if (routeOrder <= 0) throw new ArgumentException("Falta la entrega.");
        var login = OriginalLoginSession.ValidatedRequest(accessor.HttpContext ?? throw new UnauthorizedAccessException());
        return new(login.LoginId, login.UserId, login.ProfileId, login.SecurityStamp, login.BindingHash.ToArray(), login.IssuedUtc, login.ExpiresUtc);
    }
    private Task<DeliveryNotificationReceipt> Lookup(CommerceSession session, int resource, Guid sync, Guid operation, CancellationToken ct)
    {
        if (operation != Guid.Empty && sync == Guid.Empty) throw new ArgumentException("Falta el evento original.");
        return Call("receipt", new DeliveryNotificationReceiptCommand(session, resource, sync, operation), operation, sync, true, ct);
    }
    private async Task<DeliveryNotificationReceipt> Call<T>(string action, T command, Guid operation, Guid sync, bool lookup, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("internal/notifications/deliveries/" + action, command, ct);
        if (!response.IsSuccessStatusCode) throw new DeliveryNotificationTransportException((int)response.StatusCode);
        try
        {
            var result = await response.Content.ReadFromJsonAsync<DeliveryNotificationReceipt>(cancellationToken: ct);
            if (result is null || !result.IsValid || (lookup && result.State == "AlreadyRequested")
                || ((!lookup || operation != Guid.Empty) && result.OperationId != operation)
                || (sync != Guid.Empty && result.SyncGuid != sync)) throw new DeliveryNotificationTransportException(503);
            return result;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException) { throw new DeliveryNotificationTransportException(503); }
    }
}
