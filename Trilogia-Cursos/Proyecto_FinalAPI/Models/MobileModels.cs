namespace Proyecto_FinalAPI.Models
{
    // Contratos de /api/mobile/v1. Son DTO propios: no se reutilizan los
    // ViewModels del MVC, que están acoplados a las vistas Razor.

    public sealed class MobileCapabilitiesResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<MobileModule> Modules { get; set; } = Array.Empty<MobileModule>();
    }

    public sealed class MobileModule
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        /// <summary>Agrupa la pantalla de inicio (Bodega, Gestión, Oficina…).</summary>
        public string Section { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }

    public sealed class DriverRouteSummary
    {
        public int RutaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string VehiculoPlaca { get; set; } = string.Empty;
        public DateTime? FechaDespacho { get; set; }
        public int TotalPedidos { get; set; }
        public int Pendientes { get; set; }
        public int Entregados { get; set; }
    }

    public sealed class DriverRouteDetail
    {
        public int RutaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string VehiculoPlaca { get; set; } = string.Empty;
        public DateTime? FechaDespacho { get; set; }
        public List<DriverDelivery> Entregas { get; set; } = new();
    }

    public sealed class DriverDelivery
    {
        public int RutaPedidoId { get; set; }
        public int PedidoId { get; set; }
        public int Secuencia { get; set; }
        public string EstadoEntrega { get; set; } = string.Empty;
        public string MotivoFallo { get; set; } = string.Empty;
        public DateTime? FechaEntrega { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string DireccionEntrega { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int TotalEvidencias { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
    }

    public sealed class UpdateDeliveryStatusRequest
    {
        public string Estado { get; set; } = string.Empty;
        public string SyncGuid { get; set; } = string.Empty;
        public string? MotivoFallo { get; set; }
    }

    public sealed class UpdateDeliveryStatusResponse
    {
        public int RutaPedidoId { get; set; }
        public int PedidoId { get; set; }
        public string EstadoEntrega { get; set; } = string.Empty;
        public bool RutaCompletada { get; set; }
        public bool Duplicado { get; set; }
    }

    public sealed class DriverVehicle
    {
        public int VehiculoId { get; set; }
        public string Placa { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int KilometrajeActual { get; set; }
        public bool JornadaAbierta { get; set; }
    }

    public sealed class OpenMileageRequest
    {
        public int VehiculoId { get; set; }
        public int KmInicial { get; set; }
        public string SyncGuid { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
    }

    public sealed class CloseMileageRequest
    {
        public int KilometrajeId { get; set; }
        public int KmFinal { get; set; }
        public string SyncGuid { get; set; } = string.Empty;
    }

    public sealed class MileageResponse
    {
        public int KilometrajeId { get; set; }
        public int? KmFinal { get; set; }
        public int? KmRecorridos { get; set; }
        public bool Duplicado { get; set; }
    }

    public sealed class OpenMileageShift
    {
        public int KilometrajeId { get; set; }
        public int VehiculoId { get; set; }
        public string VehiculoPlaca { get; set; } = string.Empty;
        public int KmInicial { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime FechaRegistro { get; set; }
    }

    public sealed class DriverDaySummary
    {
        public int RutasActivas { get; set; }
        public int EntregasPendientes { get; set; }
        public int EntregasCompletadasHoy { get; set; }
        public int EntregasFallidasHoy { get; set; }
        public bool JornadaAbierta { get; set; }
    }

    public sealed class MobileAppVersionResponse
    {
        public int LatestBuild { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public int MinSupportedBuild { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string? MandatoryMessage { get; set; }
    }

    // Respuesta de error uniforme. Nunca lleva ex.Message.
    public sealed class MobileError
    {
        public MobileError(string error, string message)
        {
            Error = error;
            Message = message;
        }

        public string Error { get; }
        public string Message { get; }
    }
}
