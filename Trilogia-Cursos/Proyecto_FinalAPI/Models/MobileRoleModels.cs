namespace Proyecto_FinalAPI.Models
{
    // Contratos de /api/mobile/v1 para los perfiles distintos del chofer.
    // Ningún contrato de entrada lleva el identificador de quien actúa: sale
    // del token. Los identificadores que sí viajan (ClienteId, NuevoChoferId)
    // son el objetivo de la acción, no la identidad de quien la hace.

    // ── Inventario ─────────────────────────────────────────────────────────

    public sealed class MobileProduct
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public int StockMinimo { get; set; }
        public string EstadoStock { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }

    public sealed class MobileInventoryMovement
    {
        public int MovimientoId { get; set; }
        public string TipoMovimiento { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public int StockAnterior { get; set; }
        public int StockNuevo { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string UsuarioNombre { get; set; } = string.Empty;
        public DateTime? FechaMovimiento { get; set; }
    }

    public sealed class RegisterMovementRequest
    {
        public int ProductoId { get; set; }
        public string? TipoMovimiento { get; set; }
        public int Cantidad { get; set; }
        public string? Motivo { get; set; }
        public string? SyncGuid { get; set; }
    }

    public sealed class RegisterMovementResponse
    {
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public string TipoMovimiento { get; set; } = string.Empty;
        public int StockAnterior { get; set; }
        public int StockNuevo { get; set; }
        public bool Duplicado { get; set; }
    }

    public sealed class ChangeProductStatusRequest
    {
        public bool Activo { get; set; }
        public string? SyncGuid { get; set; }
    }

    public sealed class ChangeProductStatusResponse
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public bool Cambio { get; set; }
    }

    public sealed class MobilePurchaseOrder
    {
        public int OrdenCompraId { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
        public DateTime? FechaCreacion { get; set; }
        public decimal MontoTotal { get; set; }
        public int TotalOrdenado { get; set; }
        public int TotalRecibido { get; set; }
    }

    public sealed class MobilePurchaseOrderDetail
    {
        public int OrdenCompraId { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
        public DateTime? FechaCreacion { get; set; }
        public bool AdmiteRecepcion { get; set; }
        public List<MobilePurchaseOrderLine> Lineas { get; set; } = new();
    }

    public sealed class MobilePurchaseOrderLine
    {
        public int DetalleOrdenCompraId { get; set; }
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public int CantidadOrdenada { get; set; }
        public int CantidadRecibida { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int Pendiente => Math.Max(0, CantidadOrdenada - CantidadRecibida);
    }

    public sealed class ReceivePurchaseLineRequest
    {
        public int Cantidad { get; set; }
        public string? SyncGuid { get; set; }
    }

    public sealed class MobilePurchaseSuggestion
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public decimal PromedioVentaMensual { get; set; }
        public int CantidadSugerida { get; set; }
        public bool DatosInsuficientes { get; set; }
    }

    // ── Pedidos ────────────────────────────────────────────────────────────

    public sealed class MobileOrderSummary
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public DateTime? FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string TipoEntrega { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string VendedorNombre { get; set; } = string.Empty;
        public string CanalPedido { get; set; } = string.Empty;
        public bool TieneFactura { get; set; }
        public bool Preparado { get; set; }
    }

    public sealed class MobileOrderDetail
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string ClienteCorreo { get; set; } = string.Empty;
        public string ClienteTelefono { get; set; } = string.Empty;
        public DateTime? FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string TipoEntrega { get; set; } = string.Empty;
        public string DireccionEntrega { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Observaciones { get; set; } = string.Empty;
        public string VendedorNombre { get; set; } = string.Empty;
        public string CanalPedido { get; set; } = string.Empty;
        public string MotivoRechazo { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
        public bool TieneFactura { get; set; }
        public string PreparadoPorNombre { get; set; } = string.Empty;
        public DateTime? FechaPreparacion { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
        public string EstadoEntrega { get; set; } = string.Empty;

        /// <summary>Estados a los que el pedido puede pasar hoy. Espejo de
        /// sp_Admin_UpdateOrderStatus, que sigue siendo quien decide.</summary>
        public List<string> TransicionesPermitidas { get; set; } = new();
        public List<MobileOrderLine> Lineas { get; set; } = new();
    }

    public sealed class MobileOrderLine
    {
        public string Nombre { get; set; } = string.Empty;
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public int StockActual { get; set; }
        public bool EsCombo { get; set; }
    }

    public sealed class ChangeOrderStatusRequest
    {
        public string? Estado { get; set; }
    }

    public sealed class MobilePickingOrder
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public DateTime? FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string TipoEntrega { get; set; } = string.Empty;
        public string DireccionEntrega { get; set; } = string.Empty;
        public int TotalLineas { get; set; }
        public int TotalUnidades { get; set; }
        public string PreparadoPorNombre { get; set; } = string.Empty;
        public DateTime? FechaPreparacion { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
    }

    public sealed class MarkPreparedRequest
    {
        public string? Observaciones { get; set; }
        public string? SyncGuid { get; set; }
    }

    public sealed class MarkPreparedResponse
    {
        public int PedidoId { get; set; }
        public string PreparadoPorNombre { get; set; } = string.Empty;
        public DateTime? FechaPreparacion { get; set; }
        public bool Duplicado { get; set; }
    }

    public sealed class MobileRetainedOrder
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public DateTime? FechaPedido { get; set; }
        public string VendedorNombre { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string TipoEntrega { get; set; } = string.Empty;
        public int TotalLineas { get; set; }
    }

    public sealed class RejectOrderRequest
    {
        public string? Motivo { get; set; }
    }

    public sealed class OrderDecisionResponse
    {
        public int PedidoId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
    }

    // ── Ventas ─────────────────────────────────────────────────────────────

    public sealed class MobileSaleClient
    {
        public int ClienteId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
    }

    public sealed class MobileSaleProduct
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Stock { get; set; }
    }

    public sealed class MobileSellerOrder
    {
        public int PedidoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public DateTime? FechaPedido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
    }

    public sealed class CreateSaleRequest
    {
        public int ClienteId { get; set; }
        public string? TipoEntrega { get; set; }
        public string? DireccionEntrega { get; set; }
        public string? Observaciones { get; set; }
        public List<CreateSaleItem> Items { get; set; } = new();
        public string? SyncGuid { get; set; }
        public bool RegistradoSinConexion { get; set; }
    }

    public sealed class CreateSaleItem
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
    }

    public sealed class CreateSaleResponse
    {
        public int PedidoId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
        public bool Retenido { get; set; }
    }

    // ── Gestión ────────────────────────────────────────────────────────────

    public sealed class MobileDashboard
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public decimal VentasPeriodo { get; set; }
        public int FacturasPeriodo { get; set; }
        public int PedidosPeriodo { get; set; }
        public decimal TicketPromedio { get; set; }
        public int StockBajo { get; set; }
        public int ProductosAgotados { get; set; }
        public int PedidosEnRuta { get; set; }
        public decimal CobrosPendientes { get; set; }
        public int RutasPlanificadas { get; set; }
        public int RutasDespachadas { get; set; }
        public int EntregasPendientes { get; set; }
        public int EntregasCompletadas { get; set; }
        public int EntregasFallidas { get; set; }
        public int PedidosRetenidos { get; set; }
        public List<MobileSalesPoint> SerieVentas { get; set; } = new();
        public List<MobileCount> PedidosPorEstado { get; set; } = new();
        public List<MobileTopProduct> ProductosTop { get; set; } = new();
        public List<MobileProductRisk> ExistenciasEnRiesgo { get; set; } = new();
    }

    public sealed class MobileSalesPoint
    {
        public DateTime Dia { get; set; }
        public decimal Total { get; set; }
        public int Facturas { get; set; }
    }

    public sealed class MobileCount
    {
        public string Etiqueta { get; set; } = string.Empty;
        public int Cantidad { get; set; }
    }

    public sealed class MobileTopProduct
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Unidades { get; set; }
        public decimal Monto { get; set; }
    }

    public sealed class MobileProductRisk
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Stock { get; set; }
        public string EstadoStock { get; set; } = string.Empty;
    }

    public sealed class MobileManagedRoute
    {
        public int RutaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Chofer { get; set; } = string.Empty;
        public string VehiculoPlaca { get; set; } = string.Empty;
        public DateTime? FechaCreacion { get; set; }
        public DateTime? FechaDespacho { get; set; }
        public int TotalPedidos { get; set; }
        public int Entregados { get; set; }
        public int Fallidos { get; set; }
        public int Pendientes { get; set; }
    }

    public sealed class MobileManagedRouteDetail
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
        public DateTime? FechaCreacion { get; set; }
        public DateTime? FechaDespacho { get; set; }
        public bool PuedeReasignar { get; set; }
        public bool PuedeDespachar { get; set; }
        public List<MobileManagedStop> Paradas { get; set; } = new();
    }

    public sealed class MobileManagedStop
    {
        public int PedidoId { get; set; }
        public int Secuencia { get; set; }
        public string EstadoEntrega { get; set; } = string.Empty;
        public string MotivoFallo { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string DireccionEntrega { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }

    public sealed class MobileDriverOption
    {
        public int ChoferId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public int RutasAbiertas { get; set; }
    }

    public sealed class MobileVehicleOption
    {
        public int VehiculoId { get; set; }
        public string Placa { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int RutasAbiertas { get; set; }
    }

    public sealed class ReassignRouteRequest
    {
        public int NuevoChoferId { get; set; }
        public int? NuevoVehiculoId { get; set; }
        public string? Motivo { get; set; }
        public string? SyncGuid { get; set; }
    }

    public sealed class ReassignRouteResponse
    {
        public int RutaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Chofer { get; set; } = string.Empty;
        public string ChoferAnterior { get; set; } = string.Empty;
        public string VehiculoPlaca { get; set; } = string.Empty;
        public bool Duplicado { get; set; }
    }

    public sealed class DispatchRouteResponse
    {
        public int RutaId { get; set; }
        public int PedidosEnRuta { get; set; }
    }

    // ── Personal ───────────────────────────────────────────────────────────

    public sealed class MobileAttendance
    {
        public long JornadaId { get; set; }
        public string Empleado { get; set; } = string.Empty;
        public DateTime? Fecha { get; set; }
        public decimal HorasOrdinarias { get; set; }
        public decimal HorasExtra { get; set; }
        public decimal HorasAusencia { get; set; }
        public string Observaciones { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string RespuestaSupervisor { get; set; } = string.Empty;

        /// <summary>Control de concurrencia de la fila, en base64. Se devuelve
        /// tal cual al resolver: si alguien la cambió antes, la base lo rechaza.</summary>
        public string Version { get; set; } = string.Empty;
    }

    public sealed class SaveAttendanceRequest
    {
        public DateTime Fecha { get; set; }
        public decimal HorasOrdinarias { get; set; }
        public decimal HorasExtra { get; set; }
        public decimal HorasAusencia { get; set; }
        public string? Observaciones { get; set; }
        public bool Enviar { get; set; } = true;
        public string? SyncGuid { get; set; }
    }

    public sealed class ResolveAttendanceRequest
    {
        public string? Decision { get; set; }
        public string? Respuesta { get; set; }
        public string? Version { get; set; }
    }

    // ── Oficina ────────────────────────────────────────────────────────────

    public sealed class MobileSettlement
    {
        public int RutaId { get; set; }
        public string RutaCodigo { get; set; } = string.Empty;
        public decimal MontoEsperadoEfectivo { get; set; }
        public decimal MontoEsperadoOtros { get; set; }
        public decimal MontoEfectivoRecibido { get; set; }
        public decimal MontoComprobantes { get; set; }
        public decimal Diferencia { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public string LiquidadoPorNombre { get; set; } = string.Empty;
        public DateTime? FechaLiquidacion { get; set; }
        public List<MobileSettlementVoucher> Comprobantes { get; set; } = new();
    }

    public sealed class MobileSettlementVoucher
    {
        public string Tipo { get; set; } = string.Empty;
        public string Referencia { get; set; } = string.Empty;
        public decimal Monto { get; set; }
    }

    public sealed class MobileInvoice
    {
        public int FacturaId { get; set; }
        public int PedidoId { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteCorreo { get; set; } = string.Empty;
        public DateTime? FechaFactura { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Impuesto { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;
        public List<MobileInvoiceLine> Lineas { get; set; } = new();
    }

    public sealed class MobileInvoiceLine
    {
        public string ProductoNombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public sealed class MobileClientCredit
    {
        public int ClienteId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public decimal LimiteCredito { get; set; }
        public bool CreditoActivo { get; set; }
        public bool CreditoBloqueado { get; set; }
        public string MotivoBloqueo { get; set; } = string.Empty;
        public decimal DeudaActual { get; set; }
        public decimal CreditoDisponible { get; set; }
        public decimal TotalCargos { get; set; }
        public decimal TotalAbonos { get; set; }
        public DateTime? UltimoMovimiento { get; set; }
        public List<MobileCreditMovement> Movimientos { get; set; } = new();
    }

    public sealed class MobileCreditMovement
    {
        public string TipoMovimiento { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string Referencia { get; set; } = string.Empty;
        public string RegistradoPorNombre { get; set; } = string.Empty;
        public DateTime? FechaMovimiento { get; set; }
    }

    public sealed class MobileConsultation
    {
        public int ConsultaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Asunto { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string RespuestaInterna { get; set; } = string.Empty;
        public string AtendidoPorNombre { get; set; } = string.Empty;
        public DateTime? FechaAtencion { get; set; }
        public DateTime? FechaCreacion { get; set; }
    }

    public sealed class UpdateConsultationRequest
    {
        public string? Estado { get; set; }
        public string? RespuestaInterna { get; set; }
    }

    public sealed class MobileAuditEntry
    {
        public long AuditoriaId { get; set; }
        public string UsuarioNombre { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string Modulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime? FechaRegistro { get; set; }
    }
}
