using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    // CU-101 - Gestión de proveedores (Compras y Proveedores).
    public class SupplierListItemViewModel
    {
        public int ProveedorId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Contacto { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    // Formulario único usado para crear y editar (ProveedorId = 0 => crear).
    public class SupplierFormViewModel
    {
        public int ProveedorId { get; set; }

        [Required(ErrorMessage = "El nombre del proveedor es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Contacto { get; set; }

        [StringLength(30)]
        public string? Telefono { get; set; }

        [StringLength(150)]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string? Email { get; set; }

        public bool Activo { get; set; } = true;
    }

    public class SuppliersIndexViewModel
    {
        public List<SupplierListItemViewModel> Proveedores { get; set; } = new();
        public SupplierFormViewModel Nuevo { get; set; } = new();
        public bool? SoloActivos { get; set; }
        public string? Filtro { get; set; }
    }
}
