using System.Net.Http.Json;
using System.Text.Json;
using JJ.Commerce;

namespace Proyecto_Final.Services;

public interface IRouteDispatchNotificationClient
{
    Task<RouteDispatchNotificationReceipt> NotifyAsync(int routeId, Guid dispatchId, long eventId, Guid eventGuid, CancellationToken ct);
    Task<RouteDispatchNotificationReceipt> ReceiptAsync(int routeId, Guid dispatchId, long eventId, Guid eventGuid, Guid operation, CancellationToken ct);
}
public sealed class RouteDispatchNotificationTransportException(int status) : Exception("El aviso de despacho no quedó confirmado.")
{ public int Status { get; } = status; }
public sealed class DisabledRouteDispatchNotificationClient : IRouteDispatchNotificationClient
{
    public Task<RouteDispatchNotificationReceipt> NotifyAsync(int routeId, Guid dispatchId, long eventId, Guid eventGuid, CancellationToken ct) => throw new RouteDispatchNotificationTransportException(503);
    public Task<RouteDispatchNotificationReceipt> ReceiptAsync(int routeId, Guid dispatchId, long eventId, Guid eventGuid, Guid operation, CancellationToken ct) => throw new RouteDispatchNotificationTransportException(503);
}

public sealed class RouteDispatchNotificationClient(HttpClient http, IHttpContextAccessor accessor, IConfiguration configuration) : IRouteDispatchNotificationClient
{
    public async Task<RouteDispatchNotificationReceipt> NotifyAsync(int routeId, Guid dispatchId, long eventId, Guid eventGuid, CancellationToken ct)
    {
        var session = Session(routeId, dispatchId, eventId, eventGuid);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(18));
        var known = await Lookup(session, routeId, dispatchId, eventId, eventGuid, Guid.Empty, deadline.Token);
        if (known.State is not ("None" or "Pending")) return known;
        Guid operation = known.OperationId;
        if (operation == Guid.Empty) do { operation = Guid.NewGuid(); } while (operation == eventGuid || operation == dispatchId);
        var result = await Call("send", new RouteDispatchNotificationCommand(session, routeId, dispatchId, eventId, eventGuid, operation), operation, eventGuid, false, deadline.Token);
        if (result.State == "AlreadyRequested")
        {
            var own = await Lookup(session, routeId, dispatchId, eventId, eventGuid, Guid.Empty, deadline.Token);
            if (own.State != "None") return own;
        }
        return result;
    }
    public Task<RouteDispatchNotificationReceipt> ReceiptAsync(int routeId, Guid dispatchId, long eventId, Guid eventGuid, Guid operation, CancellationToken ct) =>
        Lookup(Session(routeId, dispatchId, eventId, eventGuid), routeId, dispatchId, eventId, eventGuid, operation, ct);

    private CommerceSession Session(int route, Guid dispatch, long eventId, Guid eventGuid)
    {
        if (!configuration.GetValue<bool>("RouteDispatchNotifications:Enabled")) throw new RouteDispatchNotificationTransportException(503);
        if (route <= 0 || dispatch == Guid.Empty || eventId <= 0 || eventGuid == Guid.Empty || eventGuid == dispatch) throw new ArgumentException("Falta el evento de despacho.");
        var login = OriginalLoginSession.ValidatedRequest(accessor.HttpContext ?? throw new UnauthorizedAccessException());
        return new(login.LoginId, login.UserId, login.ProfileId, login.SecurityStamp, login.BindingHash.ToArray(), login.IssuedUtc, login.ExpiresUtc);
    }
    private Task<RouteDispatchNotificationReceipt> Lookup(CommerceSession session, int route, Guid dispatch, long eventId, Guid eventGuid, Guid operation, CancellationToken ct) =>
        Call("receipt", new RouteDispatchNotificationReceiptCommand(session, route, dispatch, eventId, eventGuid, operation), operation, eventGuid, true, ct);
    private async Task<RouteDispatchNotificationReceipt> Call<T>(string action, T command, Guid operation, Guid eventGuid, bool lookup, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("internal/notifications/route-dispatch/" + action, command, ct);
        if (!response.IsSuccessStatusCode) throw new RouteDispatchNotificationTransportException((int)response.StatusCode);
        try
        {
            var result = await response.Content.ReadFromJsonAsync<RouteDispatchNotificationReceipt>(cancellationToken: ct);
            bool noRecipient = result is { OperationId: var id, State: "NoRecipient" } && id == Guid.Empty;
            if (result is null || !result.IsValid || result.EventGuid != eventGuid || lookup && result.State == "AlreadyRequested"
                || (!lookup || operation != Guid.Empty) && result.OperationId != operation && !noRecipient) throw new RouteDispatchNotificationTransportException(503);
            return result;
        }
        catch (Exception error) when (error is JsonException or NotSupportedException) { throw new RouteDispatchNotificationTransportException(503); }
    }
}
