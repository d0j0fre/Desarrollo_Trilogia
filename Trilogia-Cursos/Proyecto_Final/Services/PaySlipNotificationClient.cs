using System.Net.Http.Json;
using System.Text.Json;
using JJ.Commerce;

namespace Proyecto_Final.Services;

public interface IPaySlipNotificationClient
{
    Task<PaySlipNotificationReceipt> SendAsync(long calculation, Guid operation, CancellationToken ct);
    Task<PaySlipNotificationReceipt> ReceiptAsync(long calculation, Guid operation, CancellationToken ct);
}

public sealed class PaySlipNotificationTransportException(int status)
    : Exception("El envío de la boleta no quedó confirmado.")
{
    public int Status { get; } = status;
}

public sealed class DisabledPaySlipNotificationClient : IPaySlipNotificationClient
{
    public Task<PaySlipNotificationReceipt> SendAsync(long calculation, Guid operation, CancellationToken ct)
        => throw new PaySlipNotificationTransportException(503);
    public Task<PaySlipNotificationReceipt> ReceiptAsync(long calculation, Guid operation, CancellationToken ct)
        => throw new PaySlipNotificationTransportException(503);
}

/// <summary>Requires the authority's configured mTLS HttpClient. Only the validated original login and selectors cross the boundary.</summary>
public sealed class PaySlipNotificationClient(HttpClient http, IHttpContextAccessor accessor, IConfiguration configuration)
    : IPaySlipNotificationClient
{
    private const int ResponseLimit = 4 * 1024;
    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };

    public Task<PaySlipNotificationReceipt> SendAsync(long calculation, Guid operation, CancellationToken ct)
    {
        ValidateSelectors(calculation, operation);
        return CallAsync("send", new PaySlipNotificationCommand(Session(), calculation, operation), operation, ct);
    }

    public Task<PaySlipNotificationReceipt> ReceiptAsync(long calculation, Guid operation, CancellationToken ct)
    {
        ValidateSelectors(calculation, operation);
        return CallAsync("receipt", new PaySlipNotificationReceiptCommand(Session(), calculation, operation), operation, ct);
    }

    private static void ValidateSelectors(long calculation, Guid operation)
    {
        if (calculation <= 0 || operation == Guid.Empty) throw new ArgumentException("Falta el intento original de la boleta.");
    }

    private CommerceSession Session()
    {
        if (!configuration.GetValue<bool>("PaySlipNotifications:Enabled")) throw new PaySlipNotificationTransportException(503);
        var login = OriginalLoginSession.ValidatedRequest(accessor.HttpContext
            ?? throw new UnauthorizedAccessException("Falta el contexto original validado."));
        return new(login.LoginId, login.UserId, login.ProfileId, login.SecurityStamp, login.BindingHash.ToArray(), login.IssuedUtc, login.ExpiresUtc);
    }

    private async Task<PaySlipNotificationReceipt> CallAsync<T>(string action, T command, Guid operation, CancellationToken ct)
    {
        // ResponseHeadersRead ends HttpClient.Timeout at the headers; this deadline also covers the body.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var token = deadline.Token;
        // A lost response must be recovered through Receipt using the same UUID, never by replaying SMTP.
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/notifications/pay-slips/" + action)
        { Content = JsonContent.Create(command) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new PaySlipNotificationTransportException((int)response.StatusCode);
        if (response.Content.Headers.ContentLength is > ResponseLimit) throw new PaySlipNotificationTransportException(503);
        try
        {
            // One extra byte distinguishes an exact-limit response from a truncated oversized body.
            byte[] bytes = new byte[ResponseLimit + 1];
            int length = 0;
            await using var input = await response.Content.ReadAsStreamAsync(token);
            while (length < bytes.Length)
            {
                int read = await input.ReadAsync(bytes.AsMemory(length), token);
                if (read == 0) break;
                length += read;
            }
            if (length > ResponseLimit) throw new PaySlipNotificationTransportException(503);
            var result = JsonSerializer.Deserialize<PaySlipNotificationReceipt>(bytes.AsSpan(0, length), ResponseJson);
            if (result is null || !result.IsValid || result.OperationId != operation)
                throw new PaySlipNotificationTransportException(503);
            return result;
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        { throw new PaySlipNotificationTransportException(503); }
    }
}
