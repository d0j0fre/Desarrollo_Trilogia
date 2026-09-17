import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../../core/network/api_client.dart';
import '../../../core/providers.dart';
import '../../../shared/utils/formatters.dart';

// ── Métricas ─────────────────────────────────────────────────────────────────

class Dashboard {
  const Dashboard({
    required this.from,
    required this.to,
    required this.sales,
    required this.invoices,
    required this.orders,
    required this.averageTicket,
    required this.lowStock,
    required this.outOfStock,
    required this.ordersInRoute,
    required this.receivables,
    required this.plannedRoutes,
    required this.dispatchedRoutes,
    required this.pendingDeliveries,
    required this.completedDeliveries,
    required this.failedDeliveries,
    required this.retainedOrders,
    required this.series,
    required this.ordersByStatus,
    required this.topProducts,
    required this.stockRisk,
  });

  final DateTime? from;
  final DateTime? to;
  final double sales;
  final int invoices;
  final int orders;
  final double averageTicket;
  final int lowStock;
  final int outOfStock;
  final int ordersInRoute;
  final double receivables;
  final int plannedRoutes;
  final int dispatchedRoutes;
  final int pendingDeliveries;
  final int completedDeliveries;
  final int failedDeliveries;
  final int retainedOrders;
  final List<SalesPoint> series;
  final List<(String, int)> ordersByStatus;
  final List<TopProduct> topProducts;
  final List<(String, int, String)> stockRisk;

  factory Dashboard.fromJson(Map<String, dynamic> json) => Dashboard(
        from: J.date(json['desde']),
        to: J.date(json['hasta']),
        sales: J.decimal(json['ventasPeriodo']),
        invoices: J.integer(json['facturasPeriodo']),
        orders: J.integer(json['pedidosPeriodo']),
        averageTicket: J.decimal(json['ticketPromedio']),
        lowStock: J.integer(json['stockBajo']),
        outOfStock: J.integer(json['productosAgotados']),
        ordersInRoute: J.integer(json['pedidosEnRuta']),
        receivables: J.decimal(json['cobrosPendientes']),
        plannedRoutes: J.integer(json['rutasPlanificadas']),
        dispatchedRoutes: J.integer(json['rutasDespachadas']),
        pendingDeliveries: J.integer(json['entregasPendientes']),
        completedDeliveries: J.integer(json['entregasCompletadas']),
        failedDeliveries: J.integer(json['entregasFallidas']),
        retainedOrders: J.integer(json['pedidosRetenidos']),
        series: J.list(json['serieVentas'], SalesPoint.fromJson),
        ordersByStatus: J.list(json['pedidosPorEstado'], (m) => (J.text(m['etiqueta']), J.integer(m['cantidad']))),
        topProducts: J.list(json['productosTop'], TopProduct.fromJson),
        stockRisk: J.list(json['existenciasEnRiesgo'],
            (m) => (J.text(m['nombre']), J.integer(m['stock']), J.text(m['estadoStock']))),
      );
}

class SalesPoint {
  const SalesPoint(this.day, this.total, this.invoices);

  final DateTime day;
  final double total;
  final int invoices;

  factory SalesPoint.fromJson(Map<String, dynamic> json) =>
      SalesPoint(J.date(json['dia']) ?? DateTime.now(), J.decimal(json['total']), J.integer(json['facturas']));
}

class TopProduct {
  const TopProduct(this.name, this.units, this.amount);

  final String name;
  final int units;
  final double amount;

  factory TopProduct.fromJson(Map<String, dynamic> json) =>
      TopProduct(J.text(json['nombre']), J.integer(json['unidades']), J.decimal(json['monto']));
}

// ── Rutas ────────────────────────────────────────────────────────────────────

class ManagedRoute {
  const ManagedRoute({
    required this.id,
    required this.code,
    required this.zone,
    required this.status,
    required this.driver,
    required this.plate,
    required this.dispatchedAt,
    required this.total,
    required this.delivered,
    required this.failed,
    required this.pending,
  });

  final int id;
  final String code;
  final String zone;
  final String status;
  final String driver;
  final String plate;
  final DateTime? dispatchedAt;
  final int total;
  final int delivered;
  final int failed;
  final int pending;

  double get progress => total == 0 ? 0 : ((delivered + failed) / total).clamp(0, 1);

  factory ManagedRoute.fromJson(Map<String, dynamic> json) => ManagedRoute(
        id: J.integer(json['rutaId']),
        code: J.text(json['codigo']),
        zone: J.text(json['zona']),
        status: J.text(json['estado']),
        driver: J.text(json['chofer']),
        plate: J.text(json['vehiculoPlaca']),
        dispatchedAt: J.date(json['fechaDespacho']),
        total: J.integer(json['totalPedidos']),
        delivered: J.integer(json['entregados']),
        failed: J.integer(json['fallidos']),
        pending: J.integer(json['pendientes']),
      );
}

class ManagedRouteDetail {
  const ManagedRouteDetail({
    required this.id,
    required this.code,
    required this.zone,
    required this.status,
    required this.driverId,
    required this.driver,
    required this.vehicleId,
    required this.plate,
    required this.vehicleDescription,
    required this.notes,
    required this.dispatchedAt,
    required this.canReassign,
    required this.canDispatch,
    required this.stops,
  });

  final int id;
  final String code;
  final String zone;
  final String status;
  final int driverId;
  final String driver;
  final int vehicleId;
  final String plate;
  final String vehicleDescription;
  final String notes;
  final DateTime? dispatchedAt;
  final bool canReassign;
  final bool canDispatch;
  final List<ManagedStop> stops;

  factory ManagedRouteDetail.fromJson(Map<String, dynamic> json) => ManagedRouteDetail(
        id: J.integer(json['rutaId']),
        code: J.text(json['codigo']),
        zone: J.text(json['zona']),
        status: J.text(json['estado']),
        driverId: J.integer(json['choferUsuarioId']),
        driver: J.text(json['chofer']),
        vehicleId: J.integer(json['vehiculoId']),
        plate: J.text(json['vehiculoPlaca']),
        vehicleDescription: J.text(json['vehiculoDescripcion']),
        notes: J.text(json['observaciones']),
        dispatchedAt: J.date(json['fechaDespacho']),
        canReassign: J.boolean(json['puedeReasignar']),
        canDispatch: J.boolean(json['puedeDespachar']),
        stops: J.list(json['paradas'], ManagedStop.fromJson),
      );
}

class ManagedStop {
  const ManagedStop(this.orderId, this.sequence, this.status, this.failureReason, this.client, this.address, this.total);

  final int orderId;
  final int sequence;
  final String status;
  final String failureReason;
  final String client;
  final String address;
  final double total;

  factory ManagedStop.fromJson(Map<String, dynamic> json) => ManagedStop(
        J.integer(json['pedidoId']),
        J.integer(json['secuencia']),
        J.text(json['estadoEntrega']),
        J.text(json['motivoFallo']),
        J.text(json['cliente']),
        J.text(json['direccionEntrega']),
        J.decimal(json['total']),
      );
}

class DriverOption {
  const DriverOption(this.id, this.name, this.phone, this.openRoutes);

  final int id;
  final String name;
  final String phone;
  final int openRoutes;

  factory DriverOption.fromJson(Map<String, dynamic> json) => DriverOption(
      J.integer(json['choferId']), J.text(json['nombre']), J.text(json['telefono']), J.integer(json['rutasAbiertas']));
}

class VehicleOption {
  const VehicleOption(this.id, this.plate, this.description, this.openRoutes);

  final int id;
  final String plate;
  final String description;
  final int openRoutes;

  factory VehicleOption.fromJson(Map<String, dynamic> json) => VehicleOption(
      J.integer(json['vehiculoId']), J.text(json['placa']), J.text(json['descripcion']), J.integer(json['rutasAbiertas']));
}

// ── Pedidos ──────────────────────────────────────────────────────────────────

class OrderSummary {
  const OrderSummary({
    required this.id,
    required this.client,
    required this.date,
    required this.status,
    required this.deliveryType,
    required this.total,
    required this.seller,
    required this.invoiced,
    required this.prepared,
  });

  final int id;
  final String client;
  final DateTime? date;
  final String status;
  final String deliveryType;
  final double total;
  final String seller;
  final bool invoiced;
  final bool prepared;

  factory OrderSummary.fromJson(Map<String, dynamic> json) => OrderSummary(
        id: J.integer(json['pedidoId']),
        client: J.text(json['cliente']),
        date: J.date(json['fechaPedido']),
        status: J.text(json['estado']),
        deliveryType: J.text(json['tipoEntrega']),
        total: J.decimal(json['total']),
        seller: J.text(json['vendedorNombre']),
        invoiced: J.boolean(json['tieneFactura']),
        prepared: J.boolean(json['preparado']),
      );
}

class OrderDetail {
  const OrderDetail({
    required this.id,
    required this.client,
    required this.clientEmail,
    required this.clientPhone,
    required this.date,
    required this.status,
    required this.deliveryType,
    required this.address,
    required this.total,
    required this.notes,
    required this.seller,
    required this.channel,
    required this.rejectionReason,
    required this.invoiceNumber,
    required this.invoiced,
    required this.preparedBy,
    required this.preparedAt,
    required this.routeCode,
    required this.deliveryStatus,
    required this.allowedTransitions,
    required this.lines,
  });

  final int id;
  final String client;
  final String clientEmail;
  final String clientPhone;
  final DateTime? date;
  final String status;
  final String deliveryType;
  final String address;
  final double total;
  final String notes;
  final String seller;
  final String channel;
  final String rejectionReason;
  final String invoiceNumber;
  final bool invoiced;
  final String preparedBy;
  final DateTime? preparedAt;
  final String routeCode;
  final String deliveryStatus;
  final List<String> allowedTransitions;
  final List<OrderLine> lines;

  factory OrderDetail.fromJson(Map<String, dynamic> json) => OrderDetail(
        id: J.integer(json['pedidoId']),
        client: J.text(json['cliente']),
        clientEmail: J.text(json['clienteCorreo']),
        clientPhone: J.text(json['clienteTelefono']),
        date: J.date(json['fechaPedido']),
        status: J.text(json['estado']),
        deliveryType: J.text(json['tipoEntrega']),
        address: J.text(json['direccionEntrega']),
        total: J.decimal(json['total']),
        notes: J.text(json['observaciones']),
        seller: J.text(json['vendedorNombre']),
        channel: J.text(json['canalPedido']),
        rejectionReason: J.text(json['motivoRechazo']),
        invoiceNumber: J.text(json['numeroFactura']),
        invoiced: J.boolean(json['tieneFactura']),
        preparedBy: J.text(json['preparadoPorNombre']),
        preparedAt: J.date(json['fechaPreparacion']),
        routeCode: J.text(json['rutaCodigo']),
        deliveryStatus: J.text(json['estadoEntrega']),
        allowedTransitions: (json['transicionesPermitidas'] as List<dynamic>? ?? []).map((e) => '$e').toList(),
        lines: J.list(json['lineas'], OrderLine.fromJson),
      );
}

class OrderLine {
  const OrderLine(this.name, this.quantity, this.unitPrice, this.subtotal, this.stock, this.isCombo);

  final String name;
  final int quantity;
  final double unitPrice;
  final double subtotal;
  final int stock;
  final bool isCombo;

  factory OrderLine.fromJson(Map<String, dynamic> json) => OrderLine(
        J.text(json['nombre']),
        J.integer(json['cantidad']),
        J.decimal(json['precioUnitario']),
        J.decimal(json['subtotal']),
        J.integer(json['stockActual']),
        J.boolean(json['esCombo']),
      );
}

class RetainedOrder {
  const RetainedOrder(this.id, this.client, this.date, this.seller, this.total, this.deliveryType, this.lines);

  final int id;
  final String client;
  final DateTime? date;
  final String seller;
  final double total;
  final String deliveryType;
  final int lines;

  factory RetainedOrder.fromJson(Map<String, dynamic> json) => RetainedOrder(
        J.integer(json['pedidoId']),
        J.text(json['cliente']),
        J.date(json['fechaPedido']),
        J.text(json['vendedorNombre']),
        J.decimal(json['total']),
        J.text(json['tipoEntrega']),
        J.integer(json['totalLineas']),
      );
}

// ── Acceso ───────────────────────────────────────────────────────────────────

/// Gestión rápida. Todo en línea, sin cola: aprobar, reasignar o despachar son
/// decisiones que dependen del estado de este momento, y quien las toma
/// necesita la confirmación del servidor antes de seguir.
class ManagementApi {
  ManagementApi(this._api, this._uuid);

  final ApiClient _api;
  final Uuid _uuid;

  Future<Dashboard> dashboard(String range) async =>
      Dashboard.fromJson(await _api.get<Map<String, dynamic>>('api/mobile/v1/management/dashboard', query: {'rango': range}));

  Future<List<ManagedRoute>> routes({String status = '', String search = ''}) async {
    final data = await _api.get<List<dynamic>>('api/mobile/v1/management/routes', query: {
      if (status.isNotEmpty) 'estado': status,
      if (search.isNotEmpty) 'buscar': search,
    });
    return data.map((item) => ManagedRoute.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<ManagedRouteDetail> route(int id) async =>
      ManagedRouteDetail.fromJson(await _api.get<Map<String, dynamic>>('api/mobile/v1/management/routes/$id'));

  Future<List<DriverOption>> drivers() async => (await _api.get<List<dynamic>>('api/mobile/v1/management/drivers'))
      .map((item) => DriverOption.fromJson(item as Map<String, dynamic>))
      .toList();

  Future<List<VehicleOption>> vehicles() async => (await _api.get<List<dynamic>>('api/mobile/v1/management/vehicles'))
      .map((item) => VehicleOption.fromJson(item as Map<String, dynamic>))
      .toList();

  Future<Map<String, dynamic>> reassign({required int routeId, required int driverId, int? vehicleId, required String reason}) =>
      _api.post<Map<String, dynamic>>('api/mobile/v1/management/routes/$routeId/reassign', body: {
        'nuevoChoferId': driverId,
        'nuevoVehiculoId': ?vehicleId,
        'motivo': reason,
        'syncGuid': _uuid.v4(),
      });

  Future<int> dispatch(int routeId) async {
    final data = await _api.post<Map<String, dynamic>>('api/mobile/v1/management/routes/$routeId/dispatch');
    return J.integer(data['pedidosEnRuta']);
  }

  Future<List<OrderSummary>> orders({String status = '', String search = ''}) async {
    final data = await _api.get<List<dynamic>>('api/mobile/v1/orders', query: {
      if (status.isNotEmpty) 'estado': status,
      if (search.isNotEmpty) 'buscar': search,
    });
    return data.map((item) => OrderSummary.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<OrderDetail> order(int id) async =>
      OrderDetail.fromJson(await _api.get<Map<String, dynamic>>('api/mobile/v1/orders/$id'));

  Future<OrderDetail> changeStatus(int id, String status) async => OrderDetail.fromJson(
      await _api.post<Map<String, dynamic>>('api/mobile/v1/orders/$id/status', body: {'estado': status}));

  Future<List<RetainedOrder>> retained() async => (await _api.get<List<dynamic>>('api/mobile/v1/orders/retained'))
      .map((item) => RetainedOrder.fromJson(item as Map<String, dynamic>))
      .toList();

  Future<String> approve(int id) async {
    final data = await _api.post<Map<String, dynamic>>('api/mobile/v1/orders/$id/approve');
    return J.text(data['numeroFactura']);
  }

  Future<void> reject(int id, String reason) =>
      _api.post<Map<String, dynamic>>('api/mobile/v1/orders/$id/reject', body: {'motivo': reason});
}

final managementApiProvider = Provider<ManagementApi>(
  (ref) => ManagementApi(ref.watch(apiClientProvider), ref.watch(uuidProvider)),
);
