using System.Net.Mail;

namespace Proyecto_FinalAPI.Services;

public static class ContactNotificationConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Contact:NotificationsEnabled")) return;
        if (!configuration.GetValue<bool>("Commerce:Enabled")
            || !MailAddress.TryCreate(configuration["Contact:NotificationRecipient"], out var recipient)
            || recipient.Address != configuration["Contact:NotificationRecipient"]?.Trim())
            throw new InvalidOperationException("La notificación de consultas requiere identidad TLS y destinatario canónico.");
    }
}
