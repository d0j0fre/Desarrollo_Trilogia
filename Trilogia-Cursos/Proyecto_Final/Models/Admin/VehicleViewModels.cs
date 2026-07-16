using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    public class VehicleListItemViewModel
    {
        public int VehiculoId { get; set; }
        public string Placa { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int Capacidad { get; set; }
        public bool Activo { get; set; }
        public int RutasAbiertas { get; set; }
    }

    public class VehicleFormViewModel
    {
        public int VehiculoId { get; set; }

        [Required(ErrorMessage = "La placa es obligatoria.")]
        [StringLength(20, ErrorMessage = "La placa no puede exceder 20 caracteres.")]
        public string Placa { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(150, ErrorMessage = "La descripción no puede exceder 150 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        [Range(0, 100000, ErrorMessage = "La capacidad debe ser un número válido.")]
        public int Capacidad { get; set; }

        public bool Activo { get; set; } = true;
    }
}
