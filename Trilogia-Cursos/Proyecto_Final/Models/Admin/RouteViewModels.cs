using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    // ── Listado de rutas ────────────────────────────────────
    public class RouteListItemViewModel
    {
        public int RutaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Chofer { get; set; } = string.Empty;
        public string VehiculoPlaca { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaDespacho { get; set; }
        public int TotalPedidos { get; set; }
        public int Entregados { get; set; }
        public int Fallidos { get; set; }
        public int Pendientes { get; set; }
    }

    // ── Cabecera de la ruta ─────────────────────────────────
    public class RouteHeaderViewModel
    {
        public int RutaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public int ChoferUsuarioId { get; set; }
        public string Chofer { get; set; } = string.Empty;
        public int VehiculoId { get; set; }
        public string VehiculoPlaca { get; set; } = string.Empty;
        public string VehiculoDescripcion { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public string CreadaPorNombre { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaDespacho { get; set; }
        public DateTime? FechaCierre { get; set; }
    }

    // ── Pedido dentro de una ruta ───────────────────────────
    public class RouteOrderItemViewModel
    {
        public int RutaPedidoId { get; set; }
        public int PedidoId { get; set; }
        public int Secuencia { get; set; }
        public string EstadoEntrega { get; set; } = string.Empty;
        public string MotivoFallo { get; set; } = string.Empty;
        public DateTime? FechaEntrega { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string ClienteCorreo { get; set; } = string.Empty;
        public string DireccionEntrega { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string EstadoPedido { get; set; } = string.Empty;
        public int TotalEvidencias { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public int Prioridad { get; set; } = RoutePriority.Normal;
    }

    // Prioridad de una parada (BUG-10-01). La secuenciación automática visita primero las
    // paradas de prioridad 1, luego las de 2 y al final las de 3; dentro de cada nivel
    // sigue el vecino más cercano. Los valores son los que guarda RutaPedidos.Prioridad.
    public static class RoutePriority
    {
        public const int Urgente = 1;
        public const int Alta = 2;
        public const int Normal = 3;

        public static IReadOnlyList<(int Valor, string Texto)> Opciones { get; } = new[]
        {
            (Urgente, "Urgente"), (Alta, "Alta"), (Normal, "Normal")
        };

        public static string Texto(int prioridad) => prioridad switch
        {
            Urgente => "Urgente",
            Alta => "Alta",
            _ => "Normal"
        };
    }

    // ── Pedido asignable a una ruta ─────────────────────────
    public class AssignableOrderViewModel
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string TipoEntrega { get; set; } = string.Empty;
        public string DireccionEntrega { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int TotalLineas { get; set; }
    }

    // ── Opciones de recursos para asignar ───────────────────
    public class DriverOptionViewModel
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public int RutasAbiertas { get; set; }
    }

    public class VehicleOptionViewModel
    {
        public int VehiculoId { get; set; }
        public string Placa { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int Capacidad { get; set; }
        public int RutasAbiertas { get; set; }
    }

    // ── Formulario de creación de ruta ──────────────────────
    public class RouteCreateViewModel
    {
        [Required(ErrorMessage = "La zona es obligatoria.")]
        [StringLength(120, ErrorMessage = "La zona no puede exceder 120 caracteres.")]
        public string Zona { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un chofer disponible.")]
        public int ChoferUsuarioId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un vehículo disponible.")]
        public int VehiculoId { get; set; }

        [StringLength(300, ErrorMessage = "Las observaciones no pueden exceder 300 caracteres.")]
        public string? Observaciones { get; set; }

        public List<int> PedidosSeleccionados { get; set; } = new();

        // Prioridad elegida por pedido (BUG-10-01): Prioridades[pedidoId] = 1 Urgente, 2 Alta, 3 Normal.
        public Dictionary<int, int> Prioridades { get; set; } = new();

        // Datos para poblar el formulario
        public List<AssignableOrderViewModel> PedidosDisponibles { get; set; } = new();
        public List<DriverOptionViewModel> Choferes { get; set; } = new();
        public List<VehicleOptionViewModel> Vehiculos { get; set; } = new();

        public bool HayRecursos => Choferes.Count > 0 && Vehiculos.Count > 0;
    }

    // ── Detalle de ruta ─────────────────────────────────────
    public class RouteDetailViewModel
    {
        public RouteHeaderViewModel Cabecera { get; set; } = new();
        public List<RouteOrderItemViewModel> Pedidos { get; set; } = new();
        public List<AssignableOrderViewModel> PedidosDisponibles { get; set; } = new();

        // BUG-25-02: tiempo y distancia estimados de lo que falta recorrer, en el orden actual.
        public Proyecto_Final.Services.RouteEstimate Estimacion { get; set; } =
            Proyecto_Final.Services.RouteEstimator.Estimate(Array.Empty<RouteOrderItemViewModel>());
    }

    public sealed class DeliveryBoardRowViewModel
    {
        public int RutaId { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string Zona { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
        public string Chofer { get; init; } = string.Empty;
        public string VehiculoPlaca { get; init; } = string.Empty;
        public int TotalPedidos { get; init; }
        public int Entregados { get; init; }
        public int Fallidos { get; init; }
        public int Pendientes { get; init; }
        public DateTime? FechaDespacho { get; init; }
    }

    // BUG-10-02 (CU-104): un pedido del tablero, En ruta o pendiente de entrega.
    public sealed class DeliveryBoardOrderViewModel
    {
        public string Grupo { get; init; } = string.Empty;   // "EnRuta" | "Pendiente"
        public int PedidoId { get; init; }
        public string Cliente { get; init; } = string.Empty;
        public string Direccion { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public DateTime FechaPedido { get; init; }
        public int? RutaId { get; init; }                     // null: todavía sin ruta
        public string RutaCodigo { get; init; } = string.Empty;
        public string RutaEstado { get; init; } = string.Empty;
        public string Chofer { get; init; } = string.Empty;
        public int? Secuencia { get; init; }
        public int? Prioridad { get; init; }
        public string PrioridadTexto => Prioridad.HasValue ? RoutePriority.Texto(Prioridad.Value) : string.Empty;
    }

    public sealed class DeliveryBoardViewModel
    {
        public DateTimeOffset GeneradoUtc { get; init; }
        public int RutasActivas { get; init; }
        // Pedidos a domicilio que todavía no salieron (con ruta planificada o sin ruta).
        public int EntregasPendientes { get; init; }
        public int PedidosEnRuta { get; init; }
        public int EntregasCompletadas { get; init; }
        public int EntregasFallidas { get; init; }
        public IReadOnlyList<DeliveryBoardRowViewModel> Rutas { get; init; } = [];
        public IReadOnlyList<DeliveryBoardOrderViewModel> EnRuta { get; init; } = [];
        public IReadOnlyList<DeliveryBoardOrderViewModel> Pendientes { get; init; } = [];
    }
}
