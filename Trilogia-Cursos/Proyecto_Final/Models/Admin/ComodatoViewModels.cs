using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Validation;

namespace Proyecto_Final.Models.Admin
{
    // CU-162/163/164 — Comodatos (activos prestados a clientes).
    public class ComodatoListItemViewModel
    {
        public int ComodatoId { get; set; }
        public int ActivoId { get; set; }
        public string CodigoActivo { get; set; } = string.Empty;
        public string ActivoNombre { get; set; } = string.Empty;
        public string ActivoTipo { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteIdentificacion { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public DateTime FechaAsignacion { get; set; }
        public DateTime? FechaDevolucion { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string DestinoDevolucion { get; set; } = string.Empty;
        public int DiasEnComodato { get; set; }
    }

    // CU-162 — Formulario de asignación.
    public class ComodatoAssignViewModel
    {
        [Required(ErrorMessage = "Seleccione el activo a prestar.")]
        [Display(Name = "Activo")]
        public int ActivoId { get; set; }

        // BUG-16-02: el cliente se elige de la lista de clientes registrados.
        [Required(ErrorMessage = "Seleccione el cliente que recibe el equipo.")]
        [Display(Name = "Cliente")]
        public int? ClienteUsuarioId { get; set; }

        // Se toma del cliente seleccionado (no se escribe).
        [StringLength(150)]
        public string ClienteNombre { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Identificación")]
        [CostaRicanIdentification]
        public string? ClienteIdentificacion { get; set; }

        [StringLength(200)]
        [Display(Name = "Ubicación del equipo")]
        public string? Ubicacion { get; set; }

        [Display(Name = "Fecha de asignación")]
        [DataType(DataType.Date)]
        public DateTime? FechaAsignacion { get; set; }

        [StringLength(300)]
        [Display(Name = "Condición de entrega")]
        public string? CondicionEntrega { get; set; }

        [StringLength(300)]
        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        // Opciones para el combo de activos disponibles.
        public List<AssetOptionViewModel> ActivosDisponibles { get; set; } = new();

        // BUG-16-02: clientes activos y alerta de riesgo cuando se bloquea por morosidad.
        public List<ComodatoClientOptionViewModel> Clientes { get; set; } = new();
        public ComodatoDebtViewModel? Riesgo { get; set; }
    }

    // BUG-16-02 — cliente de la lista del formulario de asignación.
    public class ComodatoClientOptionViewModel
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;
        public decimal SaldoVencido { get; set; }
        public int DiasAtraso { get; set; }
        public bool EsMoroso => SaldoVencido > 0;
    }

    // BUG-16-02 — deuda del cliente (fn_Comodato_DeudaVencida: saldo de crédito
    // con más de 30 días, abonos aplicados a los cargos más antiguos).
    public class ComodatoDebtViewModel
    {
        public string ClienteNombre { get; set; } = string.Empty;
        public decimal SaldoActual { get; set; }
        public decimal SaldoVencido { get; set; }
        public DateTime? CargoVencidoMasAntiguo { get; set; }
        public int DiasAtraso { get; set; }
        public bool EsMoroso { get; set; }
    }

    public class AssetOptionViewModel
    {
        public int ActivoId { get; set; }
        public string CodigoActivo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
    }

    // Historial de comodatos de un activo (para el detalle del activo).
    public class ComodatoHistoryItemViewModel
    {
        public int ComodatoId { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public DateTime FechaAsignacion { get; set; }
        public DateTime? FechaDevolucion { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string DestinoDevolucion { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public string CondicionEntrega { get; set; } = string.Empty;
        public string CondicionDevolucion { get; set; } = string.Empty;
    }

    // Detalle de un activo + su historial de comodatos.
    public class AssetDetailViewModel
    {
        public AssetFormViewModel Activo { get; set; } = new();
        public List<ComodatoHistoryItemViewModel> Historial { get; set; } = new();
    }

    // CU-163 — Formulario de devolución/retiro.
    public class ComodatoReturnViewModel
    {
        public int ComodatoId { get; set; }

        [Required(ErrorMessage = "Seleccione el destino del equipo.")]
        [Display(Name = "Destino")]
        public string Destino { get; set; } = "Inventario"; // Inventario | Mantenimiento

        [StringLength(300)]
        [Display(Name = "Condición de devolución")]
        public string? CondicionDevolucion { get; set; }
    }

    // CU-164 — Rentabilidad (inventario prestado cruzado con compras).
    public class ComodatoProfitabilityViewModel
    {
        public int ComodatoId { get; set; }
        public string CodigoActivo { get; set; } = string.Empty;
        public string ActivoNombre { get; set; } = string.Empty;
        public string ActivoTipo { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public int? ClienteUsuarioId { get; set; }
        public DateTime FechaAsignacion { get; set; }
        public int DiasEnComodato { get; set; }
        public int NumPedidos { get; set; }
        public decimal TotalComprado { get; set; }
        public DateTime? UltimaCompra { get; set; }
        public string Ubicacion { get; set; } = string.Empty;
        // BUG-16-04: días desde la última compra (o desde la asignación si no hay
        // compras) y alerta de baja rentabilidad a los 30 días (fn_Comodato_Rentabilidad).
        public int DiasSinCompra { get; set; }
        public bool SugerirRetiro { get; set; }
    }

    // BUG-16-01: datos del contrato de comodato (sp_Comodato_GetContrato).
    public class ComodatoContractViewModel
    {
        public int ComodatoId { get; set; }
        public string Numero => $"CMD-{ComodatoId:D5}";
        public string CodigoActivo { get; set; } = string.Empty;
        public string ActivoNombre { get; set; } = string.Empty;
        public string ActivoTipo { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteIdentificacion { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public DateTime FechaAsignacion { get; set; }
        public string CondicionEntrega { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public string RegistradoPorNombre { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    // BUG-16-04: comodato que recibió un aviso nuevo de baja rentabilidad.
    public class ComodatoProfitabilityAlertViewModel
    {
        public int ComodatoId { get; set; }
        public string CodigoActivo { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public int DiasSinCompra { get; set; }
    }
}
