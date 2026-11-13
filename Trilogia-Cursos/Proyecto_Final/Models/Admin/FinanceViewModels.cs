using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin
{
    // CU-106 — Liquidación financiera de cobros de ruta.
    public class CashSettlementListItemViewModel
    {
        public int LiquidacionCobrosId { get; set; }
        public int RutaId { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
        public decimal MontoEsperadoEfectivo { get; set; }
        public decimal MontoEsperadoOtros { get; set; }
        public decimal MontoEfectivoRecibido { get; set; }
        public decimal MontoComprobantes { get; set; }
        public decimal Diferencia { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string LiquidadoPorNombre { get; set; } = string.Empty;
        public DateTime FechaLiquidacion { get; set; }
        public string ChoferNombre { get; set; } = string.Empty;
        public bool Anulada { get; set; }
        public string MotivoAnulacion { get; set; } = string.Empty;
        public DateTime? FechaAnulacion { get; set; }
        public string AnuladaPorNombre { get; set; } = string.Empty;
    }

    // Encabezado que devuelve sp_LiquidacionCobros_Preparar.
    public class CashSettlementPrepareViewModel
    {
        public int RutaId { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
        public string EstadoRuta { get; set; } = string.Empty;
        public bool Liquidada { get; set; }
        public bool YaLiquidadaFinanciera { get; set; }
        public decimal EsperadoEfectivo { get; set; }
        public decimal EsperadoOtros { get; set; }
        public string Zona { get; set; } = string.Empty;
        public string ChoferNombre { get; set; } = string.Empty;
        public int TotalPedidos { get; set; }
        public int Entregados { get; set; }
        public List<CashSettlementOrderLine> Pedidos { get; set; } = new();
    }

    public class CashSettlementOrderLine
    {
        public int PedidoId { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; } = string.Empty;
        public string EstadoPago { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
    }

    // Formulario de registro (CU-106).
    public class CashSettlementFormViewModel
    {
        public int RutaId { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
        public decimal EsperadoEfectivo { get; set; }
        public decimal EsperadoOtros { get; set; }

        [Display(Name = "Efectivo recibido")]
        [Range(0, 99999999, ErrorMessage = "El efectivo recibido no puede ser negativo.")]
        public decimal MontoEfectivoRecibido { get; set; }

        [Display(Name = "Total en comprobantes")]
        [Range(0, 99999999, ErrorMessage = "El monto de comprobantes no puede ser negativo.")]
        public decimal MontoComprobantes { get; set; }

        [Display(Name = "Observaciones")]
        [StringLength(400)]
        public string? Observaciones { get; set; }

        // Detalle de los comprobantes: debe sumar el total (G2-38).
        public List<CashSettlementVoucher> Comprobantes { get; set; } = new();
    }

    public class CashSettlementVoucher
    {
        public string? Tipo { get; set; }
        public string? Referencia { get; set; }
        public decimal Monto { get; set; }
    }

    /// <summary>Lo que devuelve sp_LiquidacionCobros_Registrar.</summary>
    public sealed record CashSettlementResult(int LiquidacionId, string Estado, decimal Diferencia, string ChoferNombre, decimal FaltanteChofer);

    // BUG-10-05 — Faltante de una liquidación a cargo del chofer.
    public class DriverShortageViewModel
    {
        public int FaltanteId { get; set; }
        public int ChoferUsuarioId { get; set; }
        public string ChoferNombre { get; set; } = string.Empty;
        public int RutaId { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
        public int LiquidacionCobrosId { get; set; }
        public decimal Monto { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
    }

    public class FinanceIndexViewModel
    {
        public string? Estado { get; set; }
        public List<RouteListItemViewModel> RutasPendientes { get; set; } = new();
        public List<CashSettlementListItemViewModel> Liquidaciones { get; set; } = new();
        public List<DriverShortageViewModel> Faltantes { get; set; } = new();
    }

    // BUG-10-03 — Datos del recibo PDF de una liquidación.
    public class CashSettlementReceiptViewModel
    {
        public int LiquidacionCobrosId { get; set; }
        public int RutaId { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public string ChoferNombre { get; set; } = string.Empty;
        public string VehiculoPlaca { get; set; } = string.Empty;
        public string VehiculoDescripcion { get; set; } = string.Empty;
        public decimal MontoEsperadoEfectivo { get; set; }
        public decimal MontoEsperadoOtros { get; set; }
        public decimal MontoEfectivoRecibido { get; set; }
        public decimal MontoComprobantes { get; set; }
        public decimal Diferencia { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public string LiquidadoPorNombre { get; set; } = string.Empty;
        public DateTime FechaLiquidacion { get; set; }
        public bool Anulada { get; set; }
        public string MotivoAnulacion { get; set; } = string.Empty;
        public List<CashSettlementVoucher> Comprobantes { get; set; } = new();
        public List<CashSettlementReceiptOrder> Pedidos { get; set; } = new();
        public string Numero => $"LIQ-{LiquidacionCobrosId:000000}";
    }

    public class CashSettlementReceiptOrder
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }

    // ── BUG-10-07 · CU-105 — Liquidación de mercadería de una ruta completada ──
    public class RouteMerchandiseListItemViewModel
    {
        public int RutaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public string ChoferNombre { get; set; } = string.Empty;
        public DateTime? FechaCierre { get; set; }
        public int PedidosNoEntregados { get; set; }
        public int Unidades { get; set; }
        public bool Pendiente { get; set; }
        public DateTime? FechaLiquidacion { get; set; }
        public string LiquidadoPorNombre { get; set; } = string.Empty;
    }

    public class RouteMerchandisePrepareViewModel
    {
        public int RutaId { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
        public string EstadoRuta { get; set; } = string.Empty;
        public bool Liquidada { get; set; }
        public bool YaRegistrada { get; set; }
        public string Zona { get; set; } = string.Empty;
        public string ChoferNombre { get; set; } = string.Empty;
        public int PedidosNoEntregados { get; set; }
        public List<RouteMerchandiseLine> Lineas { get; set; } = new();
    }

    public class RouteMerchandiseLine
    {
        public int PedidoId { get; set; }
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        /// <summary>Unidades que salieron en la ruta en ese pedido (lo que debe volver a bodega).</summary>
        public int Cantidad { get; set; }
        /// <summary>Lo contado físicamente al recibir el camión.</summary>
        public int Contado { get; set; }
    }

    public class RouteMerchandiseFormViewModel
    {
        public int RutaId { get; set; }

        [StringLength(400)]
        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        public List<RouteMerchandiseLine> Lineas { get; set; } = new();
    }

    public sealed record RouteMerchandiseResult(int MercaderiaId, int Lineas, int Unidades);
}
