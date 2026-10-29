using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Proyecto_FinalAPI.Models;

// A contact notification has a fixed server-owned destination. It is not a general mail relay.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ContactNotificationRequest
{
    [Range(1, int.MaxValue)] public int ConsultationId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(120)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Message { get; set; } = string.Empty;
}
