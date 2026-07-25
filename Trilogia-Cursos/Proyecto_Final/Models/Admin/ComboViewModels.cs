using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    // CU-181 — Fila de la lista de combos.
    public class ComboListItemViewModel
    {
        public int ComboId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string RegistradoPorNombre { get; set; } = string.Empty;
        public int CantidadProductos { get; set; }

        // Cuántos combos se pueden armar hoy según el stock actual de sus componentes.
        public int StockDisponibleCombo { get; set; }
    }

    // CU-181 — Encabezado + componentes de un combo, para la pantalla de detalle.
    public class ComboDetailViewModel
    {
        public int ComboId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public bool Activo { get; set; }
        public string RegistradoPorNombre { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public List<ComboDetailLineViewModel> Componentes { get; set; } = new();
    }

    public class ComboDetailLineViewModel
    {
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public int StockDisponible { get; set; }
    }

    // CU-181 — Formulario para crear un combo nuevo.
    public class ComboFormViewModel
    {
        [Display(Name = "Nombre del combo")]
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Display(Name = "Descripción")]
        [StringLength(255, ErrorMessage = "La descripción no puede superar los 255 caracteres.")]
        public string? Descripcion { get; set; }

        [Display(Name = "Precio del combo")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor que cero.")]
        public decimal Precio { get; set; }

        // Lista de todos los productos activos, marcados como seleccionados si son parte del combo.
        public List<ComboProductSelectionViewModel> Productos { get; set; } = new();
    }

    public class ComboProductSelectionViewModel
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public bool Seleccionado { get; set; }
        public int Cantidad { get; set; } = 1;
    }
}
