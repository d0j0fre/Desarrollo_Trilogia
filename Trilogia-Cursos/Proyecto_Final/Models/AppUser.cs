namespace Proyecto_Final.Models
{
    public class AppUser
    {
        public int UsuarioId { get; set; }
        public int PerfilId { get; set; }
        public string PerfilNombre { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Contrasena { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }
}
