namespace Proyecto_Final.Models.Chat
{
    public class ChatDepartmentViewModel
    {
        public int PerfilId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public int TotalUsuarios { get; set; }
    }
}