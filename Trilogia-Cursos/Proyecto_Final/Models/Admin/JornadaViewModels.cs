using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    public class JornadaListItemViewModel
    {
        public long JornadaId { get; set; }
        public int EmpleadoId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public decimal HorasOrdinarias { get; set; }
        public decimal HorasExtra { get; set; }
        public decimal HorasAusencia { get; set; }
        public string? Observaciones { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? RespuestaSupervisor { get; set; }
        public string VersionFilaBase64 { get; set; } = string.Empty;
    }

    public class JornadaFormViewModel
    {
        public long? JornadaId { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [Range(0, 24, ErrorMessage = "Las horas ordinarias deben estar entre 0 y 24.")]
        [Display(Name = "Horas ordinarias")]
        public decimal HorasOrdinarias { get; set; }

        [Range(0, 24, ErrorMessage = "Las horas extra deben estar entre 0 y 24.")]
        [Display(Name = "Horas extra")]
        public decimal HorasExtra { get; set; }

        [Range(0, 24, ErrorMessage = "Las horas de ausencia deben estar entre 0 y 24.")]
        [Display(Name = "Horas de ausencia")]
        public decimal HorasAusencia { get; set; }

        [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        // true = enviar a revisión del supervisor; false = guardar como borrador
        public bool Enviar { get; set; }

        public string? VersionFilaBase64 { get; set; }
    }

    public class JornadaDecisionViewModel
    {
        public long JornadaId { get; set; }
        public string Decision { get; set; } = string.Empty; // "Aprobada" o "Rechazada"

        [StringLength(500, ErrorMessage = "La respuesta no puede superar los 500 caracteres.")]
        public string? RespuestaSupervisor { get; set; }

        public string VersionFilaBase64 { get; set; } = string.Empty;
    }

    public class JornadasAdminViewModel
    {
        public DateTime Desde { get; set; } = DateTime.Today.AddDays(-14);
        public DateTime Hasta { get; set; } = DateTime.Today;
        public List<JornadaListItemViewModel> Pendientes { get; set; } = new();
    }
}
