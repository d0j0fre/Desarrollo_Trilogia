using System.Net.Http.Json;
using Proyecto_Final.Models.Store;

namespace Proyecto_Final.Services;

public interface IContactNotificationClient
{
    Task NotifyAsync(int consultationId, ContactViewModel model, CancellationToken ct);
}

public sealed class DisabledContactNotificationClient : IContactNotificationClient
{
    public Task NotifyAsync(int consultationId, ContactViewModel model, CancellationToken ct) =>
        throw new InvalidOperationException("La notificación de consultas no está habilitada.");
}

public sealed class ContactNotificationClient(HttpClient http, IConfiguration configuration) : IContactNotificationClient
{
    public async Task NotifyAsync(int consultationId, ContactViewModel model, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Contact:NotificationsEnabled"))
            throw new InvalidOperationException("La notificación de consultas no está habilitada.");
        using var response = await http.PostAsJsonAsync("internal/notifications/contact", new
        { ConsultationId = consultationId, Name = model.Nombre, Email = model.Correo, Subject = model.Asunto, Message = model.Mensaje }, ct);
        // No automatic retry: a lost HTTP acknowledgement does not prove SMTP failure.
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("La API no confirmó la notificación de la consulta.", null, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<ContactNotificationReceipt>(cancellationToken:ct);
        if (receipt?.ConsultationId != consultationId || receipt.Status != "smtpAccepted")
            throw new HttpRequestException("La confirmación de notificación no corresponde a la consulta.");
    }
    private sealed record ContactNotificationReceipt(int ConsultationId, string Status);
}
