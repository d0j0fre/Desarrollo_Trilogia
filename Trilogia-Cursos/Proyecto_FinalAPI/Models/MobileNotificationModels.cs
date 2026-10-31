namespace Proyecto_FinalAPI.Models
{
    // Avisos para quien usa la aplicación (BUG-08-01, BUG-10-08). Siempre son
    // los del usuario del token: ningún contrato recibe otro usuario.

    public sealed class MobileNotification
    {
        public int NotificacionId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string EntidadTipo { get; set; } = string.Empty;
        public int EntidadId { get; set; }
        public bool Leida { get; set; }
        public DateTime? FechaCreacion { get; set; }
    }

    public sealed class MobileNotificationsResponse
    {
        public int NoLeidas { get; set; }
        public IReadOnlyList<MobileNotification> Items { get; set; } = [];
    }
}
