namespace Proyecto_Final.Models.Admin
{
    // Avisos por usuario (migración 0221): ruta asignada o reasignada al chofer.
    public class UserNotificationViewModel
    {
        public int NotificacionId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string EntidadTipo { get; set; } = string.Empty;
        public int? EntidadId { get; set; }
        public bool Leida { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
