namespace Proyecto_Final.Models.Chat
{
    public class ChatDepartmentMessageViewModel
    {
        public int MensajeId { get; set; }

        public int PerfilId { get; set; }

        public int RemitenteId { get; set; }

        public string Contenido { get; set; } = string.Empty;

        public DateTime FechaEnvio { get; set; }

        public string RemitenteNombre { get; set; } = string.Empty;
    }
}