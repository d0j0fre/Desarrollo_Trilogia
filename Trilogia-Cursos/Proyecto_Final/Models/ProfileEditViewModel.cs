using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models
{
    public class ProfileEditViewModel
    {
        /// <summary>
        /// BUG-02-02: teléfono con 8 a 10 dígitos (en Costa Rica son 8; se admite un prefijo corto) y solo los
        /// símbolos habituales: «+», espacios, guiones y paréntesis. La misma expresión valida en el navegador.
        /// </summary>
        public const string PhonePattern = @"^(?=(?:\D*\d){8,10}\D*$)[0-9+()\- ]+$";

        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre completo no puede superar 150 caracteres.")]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [StringLength(150, ErrorMessage = "El correo no puede superar 150 caracteres.")]
        [Display(Name = "Correo")]
        public string Correo { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
        [RegularExpression(PhonePattern, ErrorMessage = "El teléfono debe tener entre 8 y 10 dígitos (solo números, espacios, guiones, paréntesis o «+»).")]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [StringLength(255, ErrorMessage = "La dirección no puede superar 255 caracteres.")]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }
    }
}
