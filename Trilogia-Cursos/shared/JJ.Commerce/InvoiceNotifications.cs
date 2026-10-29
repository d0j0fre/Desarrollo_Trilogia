using System.Text.Json.Serialization;

namespace JJ.Commerce;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InvoiceNotificationCommand(CommerceSession Session, int InvoiceId, Guid OperationId, bool IsInitial);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InvoiceNotificationReceiptCommand(CommerceSession Session, int InvoiceId, Guid OperationId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InvoiceNotificationReceipt(Guid OperationId, string State)
{
    [JsonIgnore]
    public bool IsValid => OperationId == Guid.Empty ? State == "None" : State is "Pending" or "Sending" or "SmtpAccepted" or "Unknown" or "AlreadyRequested";
}
