using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    public class ProductAdminViewModel
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public int StockMinimo { get; set; } = 5;
        public string EstadoStock { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public bool EsDestacado { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public DateTime? FechaVencimiento { get; set; }
    }

    public class ProductFormViewModel
    {
        public int ProductoId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [StringLength(100)]
        public string Categoria { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(0.01, 999999999, ErrorMessage = "El precio debe ser mayor a cero.")]
        public decimal Precio { get; set; }

        [Required(ErrorMessage = "El stock es obligatorio.")]
        [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
        public int Stock { get; set; }

        [Required(ErrorMessage = "El stock mínimo es obligatorio.")]
        [Display(Name = "Stock mínimo")]
        [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
        public int StockMinimo { get; set; } = 5;

        [Display(Name = "Ruta o URL de imagen")]
        [StringLength(255)]
        public string? ImagenUrl { get; set; }

        [Display(Name = "Subir imagen")]
        public IFormFile? ImagenArchivo { get; set; }

        [Display(Name = "Producto destacado")]
        public bool EsDestacado { get; set; }

        public bool Activo { get; set; } = true;

        [Display(Name = "Fecha de vencimiento")]
        [DataType(DataType.Date)]
        public DateTime? FechaVencimiento { get; set; }
    }

    /// <summary>Advertencia de productos próximos a vencer (CU-052, CU-053, CU-054, CU-055).</summary>
    public static class ProductExpiration
    {
        public const int DiasAviso = 30;

        /// <summary>Días que faltan para el vencimiento; negativo si ya venció y null si el producto no vence.</summary>
        public static int? DiasParaVencer(DateTime? fechaVencimiento, DateTime hoy) =>
            fechaVencimiento.HasValue ? (fechaVencimiento.Value.Date - hoy.Date).Days : null;

        /// <summary>True si el producto ya venció o vence dentro de los próximos <see cref="DiasAviso"/> días.</summary>
        public static bool RequiereAviso(DateTime? fechaVencimiento, DateTime hoy) =>
            DiasParaVencer(fechaVencimiento, hoy) is int dias && dias <= DiasAviso;

        public static string Describir(DateTime? fechaVencimiento, DateTime hoy) => DiasParaVencer(fechaVencimiento, hoy) switch
        {
            null => string.Empty,
            < 0 and var dias => $"venció hace {-dias} día(s), el {fechaVencimiento:dd/MM/yyyy}",
            0 => $"vence hoy ({fechaVencimiento:dd/MM/yyyy})",
            var dias => $"vence en {dias} día(s), el {fechaVencimiento:dd/MM/yyyy}"
        };
    }
}
