using System.Net.Http.Json;
using System.Text.Json;
using JJ.Commerce;

namespace Proyecto_Final.Services;

public interface IInvoiceNotificationClient
{
    Task<InvoiceNotificationReceipt> SendAsync(int invoice, Guid operation, bool initial, CancellationToken ct);
    Task<InvoiceNotificationReceipt> ReceiptAsync(int invoice, Guid operation, CancellationToken ct);
}

public sealed class InvoiceNotificationTransportException(int status)
    : Exception("El envío del comprobante no quedó confirmado.")
{
    public int Status { get; } = status;
}

public sealed class DisabledInvoiceNotificationClient : IInvoiceNotificationClient
{
    public Task<InvoiceNotificationReceipt> SendAsync(int invoice, Guid operation, bool initial, CancellationToken ct)
        => throw new InvoiceNotificationTransportException(503);
    public Task<InvoiceNotificationReceipt> ReceiptAsync(int invoice, Guid operation, CancellationToken ct)
        => throw new InvoiceNotificationTransportException(503);
}

/// <summary>Only selectors and the validated original login cross the mTLS boundary.</summary>
public sealed class InvoiceNotificationClient(HttpClient http, IHttpContextAccessor accessor, IConfiguration configuration)
    : IInvoiceNotificationClient
{
    public Task<InvoiceNotificationReceipt> SendAsync(int invoice, Guid operation, bool initial, CancellationToken ct)
    {
        if (invoice <= 0 || operation == Guid.Empty) throw new ArgumentException("Falta el intento original.");
        return CallAsync("send", new InvoiceNotificationCommand(Session(), invoice, operation, initial), operation, false, ct);
    }

    public Task<InvoiceNotificationReceipt> ReceiptAsync(int invoice, Guid operation, CancellationToken ct)
    {
        if (invoice <= 0) throw new ArgumentException("Falta la factura.");
        return CallAsync("receipt", new InvoiceNotificationReceiptCommand(Session(), invoice, operation), operation, true, ct);
    }

    private CommerceSession Session()
    {
        if (!configuration.GetValue<bool>("InvoiceNotifications:Enabled")) throw new InvoiceNotificationTransportException(503);
        var login = OriginalLoginSession.ValidatedRequest(accessor.HttpContext
            ?? throw new UnauthorizedAccessException("Falta el contexto original validado."));
        return new(login.LoginId, login.UserId, login.ProfileId, login.SecurityStamp, login.BindingHash.ToArray(), login.IssuedUtc, login.ExpiresUtc);
    }

    private async Task<InvoiceNotificationReceipt> CallAsync<T>(string action, T command, Guid operation, bool receipt, CancellationToken ct)
    {
        // Never replay a send when HTTP acknowledgement is lost. Receipt is a separate operation.
        using var response = await http.PostAsJsonAsync("internal/notifications/invoices/" + action, command, ct);
        if (!response.IsSuccessStatusCode) throw new InvoiceNotificationTransportException((int)response.StatusCode);
        try
        {
            var result = await response.Content.ReadFromJsonAsync<InvoiceNotificationReceipt>(cancellationToken: ct);
            if (result is null || result.State is not ("Pending" or "Sending" or "SmtpAccepted" or "Unknown" or "AlreadyRequested" or "None")
                || (result.State == "None" && (!receipt || operation != Guid.Empty || result.OperationId != Guid.Empty))
                || (result.State != "None" && result.OperationId == Guid.Empty)
                || (operation != Guid.Empty && result.OperationId != operation)
                || (receipt && result.State == "AlreadyRequested"))
                throw new InvoiceNotificationTransportException(503);
            return result;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        { throw new InvoiceNotificationTransportException(503); }
    }
}
