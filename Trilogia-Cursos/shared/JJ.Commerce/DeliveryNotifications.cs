using System.Text.Json.Serialization;

namespace JJ.Commerce;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeliveryNotificationCommand(CommerceSession Session, int RouteOrderId, Guid SyncGuid, Guid OperationId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeliveryNotificationReceiptCommand(CommerceSession Session, int RouteOrderId, Guid SyncGuid, Guid OperationId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeliveryNotificationReceipt(Guid OperationId, Guid SyncGuid, string State)
{
    [JsonIgnore]
    public bool IsValid => OperationId == Guid.Empty
        ? SyncGuid == Guid.Empty ? State == "NoEvent" : State == "None"
        : SyncGuid != Guid.Empty && OperationId != SyncGuid && State is "Pending" or "Sending" or "SmtpAccepted" or "Unknown" or "AlreadyRequested";
}
