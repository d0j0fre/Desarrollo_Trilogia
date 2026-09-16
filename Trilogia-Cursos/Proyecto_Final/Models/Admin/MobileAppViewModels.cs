using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    public sealed class MobileAppReleaseViewModel
    {
        public int VersionId { get; set; }
        public string VersionNombre { get; set; } = string.Empty;
        public int BuildNumero { get; set; }
        public int MinBuildSoportado { get; set; }
        public string UrlDescarga { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public long? TamanoBytes { get; set; }
        public string Notas { get; set; } = string.Empty;
        public string MensajeObligatorio { get; set; } = string.Empty;
        public bool Publicada { get; set; }
        public DateTime FechaPublicacion { get; set; }
        public string PublicadaPorNombre { get; set; } = string.Empty;

        /// <summary>Identificador del archivo guardado en el sistema, si se subió aquí.</summary>
        public string ArchivoAlmacenado { get; set; } = string.Empty;
        public string ArchivoNombre { get; set; } = string.Empty;

        public bool EstaEnElSistema => !string.IsNullOrWhiteSpace(ArchivoAlmacenado);

        public string TamanoLegible => TamanoBytes is null or <= 0
            ? "—"
            : $"{TamanoBytes.Value / 1024d / 1024d:0.0} MB";

        /// <summary>Huella en grupos de ocho, para poder compararla a simple vista.</summary>
        public string Sha256Legible
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Sha256)) return string.Empty;
                var partes = Enumerable
                    .Range(0, Sha256.Length / 8)
                    .Select(i => Sha256.Substring(i * 8, 8));
                return string.Join(" ", partes);
            }
        }
    }

    /// <summary>
    /// Datos que escribe quien publica. La huella y el tamaño no están aquí a
    /// propósito: los calcula el sistema al recibir el archivo, para que no
    /// haya forma de escribirlos mal.
    /// </summary>
    public sealed class MobileAppPublishViewModel
    {
        [Required(ErrorMessage = "Indicá el número de versión.")]
        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Usá el formato 1.0.0")]
        [Display(Name = "Versión")]
        public string VersionNombre { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "El número de compilación debe ser mayor que cero.")]
        [Display(Name = "Número de compilación")]
        public int BuildNumero { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La versión mínima debe ser mayor que cero.")]
        [Display(Name = "Versión mínima soportada")]
        public int MinBuildSoportado { get; set; }

        [StringLength(500)]
        [Display(Name = "Notas de la versión")]
        public string? Notas { get; set; }

        [StringLength(300)]
        [Display(Name = "Mensaje de actualización obligatoria")]
        public string? MensajeObligatorio { get; set; }
    }

    public sealed class MobileAppPublishRequest
    {
        public string VersionNombre { get; set; } = string.Empty;
        public int BuildNumero { get; set; }
        public int MinBuildSoportado { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string? UrlDescarga { get; set; }
        public string? ArchivoAlmacenado { get; set; }
        public string? ArchivoNombre { get; set; }
        public long? TamanoBytes { get; set; }
        public string? Notas { get; set; }
        public string? MensajeObligatorio { get; set; }
    }

    public sealed class MobileAppPageViewModel
    {
        public MobileAppReleaseViewModel? Vigente { get; set; }
        public List<MobileAppReleaseViewModel> Historial { get; set; } = new();
        public bool PuedePublicar { get; set; }
        public string UrlInstalacion { get; set; } = string.Empty;

        /// <summary>
        /// El archivo de la versión vigente falta del disco. Pasa si el App
        /// Service se recreó: el registro sobrevive porque vive en la base, el
        /// archivo no. Conviene decirlo en vez de ofrecer una descarga rota.
        /// </summary>
        public bool ArchivoFaltante { get; set; }
    }
}
