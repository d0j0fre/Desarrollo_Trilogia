using System.Text.Json.Serialization;

namespace JJ.Commerce;

// Selectors only. The authority derives the recipient, private portal link and content.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PaySlipNotificationCommand(CommerceSession Session, long CalculationId, Guid OperationId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PaySlipNotificationReceiptCommand(CommerceSession Session, long CalculationId, Guid OperationId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PaySlipNotificationReceipt(Guid OperationId, string State)
{
    [JsonIgnore]
    public bool IsValid => OperationId != Guid.Empty && State is "Pending" or "Sending" or "SmtpAccepted" or "Unknown";
}
