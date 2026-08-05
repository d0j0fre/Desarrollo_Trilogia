using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    public class PayrollPeriodFormViewModel
    {
        [Required(ErrorMessage = "Debe indicar el tipo de período.")]
        [Display(Name = "Tipo de período")]
        public string TipoPeriodo { get; set; } = "Quincenal"; // Quincenal o Mensual

        [Required(ErrorMessage = "La fecha inicial es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha inicial")]
        public DateTime FechaInicio { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "La fecha final es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha final")]
        public DateTime FechaFin { get; set; } = DateTime.Today;
    }

    public class PayrollPeriodListItemViewModel
    {
        public int PeriodoPlanillaId { get; set; }
        public string TipoPeriodo { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime? FechaCalculo { get; set; }
        public DateTime? FechaAprobacion { get; set; }
        public DateTime? FechaPago { get; set; }
    }

    public class PayrollDetailListItemViewModel
    {
        public int PlanillaDetalleId { get; set; }
        public int EmpleadoId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public decimal SalarioBase { get; set; }
        public decimal HorasExtraPagadas { get; set; }
        public decimal MontoHorasExtra { get; set; }
        public decimal MontoComisiones { get; set; }
        public decimal SalarioBruto { get; set; }
        public decimal DeduccionCcss { get; set; }
        public decimal DeduccionRenta { get; set; }
        public decimal TotalDeducciones { get; set; }
        public decimal SalarioNeto { get; set; }
    }

    public class PayrollReceiptViewModel : PayrollDetailListItemViewModel
    {
        public string TipoPeriodo { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
    }

    public class PayrollPeriodDetailViewModel
    {
        public PayrollPeriodListItemViewModel Periodo { get; set; } = new();
        public List<PayrollDetailListItemViewModel> Detalles { get; set; } = new();
    }

    public class PayrollRevertViewModel
    {
        public int PeriodoPlanillaId { get; set; }

        [Required(ErrorMessage = "Debe indicar el motivo de la reversión.")]
        [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
        public string MotivoReversion { get; set; } = string.Empty;
    }
}
