using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    public class AuditLogViewModel
    {
        public int AuditoriaId { get; set; }
        public int? UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = string.Empty;
        public string UsuarioCorreo { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string Modulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string? DireccionIp { get; set; }
        public string? UserAgent { get; set; }
        public DateTime FechaRegistro { get; set; }

        /// <summary>Bitácora de la que sale el registro: General, RR. HH. o Planilla.</summary>
        public string Origen { get; set; } = AuditOrigins.GeneralLabel;
    }

    /// <summary>Bitácoras que muestra la pantalla Auditoría (G2-29). Las de RR. HH. y planilla son de solo lectura.</summary>
    public static class AuditOrigins
    {
        public const string All = "todos";
        public const string General = "general";
        public const string HumanResources = "rrhh";
        public const string Payroll = "planilla";

        public const string GeneralLabel = "General";

        public static readonly IReadOnlyList<(string Code, string Label)> Options =
        [
            (All, "Todas las bitácoras"),
            (General, "Registro general"),
            (HumanResources, "RR. HH."),
            (Payroll, "Planilla")
        ];

        /// <summary>Normaliza el origen recibido por URL; cualquier valor desconocido equivale a «todos».</summary>
        public static string Normalize(string? origin)
        {
            var value = origin?.Trim().ToLowerInvariant();
            return Options.Any(option => option.Code == value) ? value! : All;
        }
    }

    public class AuditLogFilterViewModel
    {
        [Display(Name = "Origen")]
        public string Origen { get; set; } = AuditOrigins.All;

        [Display(Name = "Módulo")]
        public string? Modulo { get; set; }

        [Display(Name = "Acción")]
        public string? Accion { get; set; }

        [Display(Name = "Buscar")]
        public string? Buscar { get; set; }

        /// <summary>Valores reales del registro para la lista Módulo.</summary>
        public IReadOnlyList<string> Modulos { get; set; } = Array.Empty<string>();

        /// <summary>Valores reales del registro para la lista Acción.</summary>
        public IReadOnlyList<string> Acciones { get; set; } = Array.Empty<string>();

        public List<AuditLogViewModel> Registros { get; set; } = new();
    }
}
