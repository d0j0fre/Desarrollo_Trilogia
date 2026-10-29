using System.Text.Json.Serialization;

namespace JJ.Commerce;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RouteDispatchNotificationCommand(CommerceSession Session, int RouteId, Guid DispatchId, long EventId, Guid EventGuid, Guid OperationId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RouteDispatchNotificationReceiptCommand(CommerceSession Session, int RouteId, Guid DispatchId, long EventId, Guid EventGuid, Guid OperationId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RouteDispatchNotificationReceipt(Guid OperationId, Guid EventGuid, string State)
{
    [JsonIgnore]
    public bool IsValid => OperationId == Guid.Empty
        ? EventGuid == Guid.Empty ? State == "NoEvent" : State is "None" or "NoRecipient"
        : EventGuid != Guid.Empty && OperationId != EventGuid && State is "Pending" or "Sending" or "SmtpAccepted" or "Unknown" or "AlreadyRequested";
}
