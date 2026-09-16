import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../../core/network/api_client.dart';
import '../../../core/providers.dart';
import '../../../core/sync/outbox_entry.dart';
import '../../../core/sync/sync_service.dart';
import '../../../shared/utils/formatters.dart';

// ── Modelos ──────────────────────────────────────────────────────────────────

class Product {
  const Product({
    required this.id,
    required this.name,
    required this.category,
    required this.price,
    required this.stock,
    required this.minStock,
    required this.stockStatus,
    required this.active,
  });

  final int id;
  final String name;
  final String category;
  final double price;
  final int stock;
  final int minStock;
  final String stockStatus;
  final bool active;

  factory Product.fromJson(Map<String, dynamic> json) => Product(
        id: J.integer(json['productoId']),
        name: J.text(json['nombre']),
        category: J.text(json['categoria']),
        price: J.decimal(json['precio']),
        stock: J.integer(json['stock']),
        minStock: J.integer(json['stockMinimo']),
        stockStatus: J.text(json['estadoStock']),
        active: J.boolean(json['activo']),
      );

  Product copyWith({bool? active, int? stock}) => Product(
        id: id,
        name: name,
        category: category,
        price: price,
        stock: stock ?? this.stock,
        minStock: minStock,
        stockStatus: stockStatus,
        active: active ?? this.active,
      );
}

class InventoryMovement {
  const InventoryMovement({
    required this.type,
    required this.quantity,
    required this.before,
    required this.after,
    required this.reason,
    required this.user,
    required this.date,
  });

  final String type;
  final int quantity;
  final int before;
  final int after;
  final String reason;
  final String user;
  final DateTime? date;

  factory InventoryMovement.fromJson(Map<String, dynamic> json) => InventoryMovement(
        type: J.text(json['tipoMovimiento']),
        quantity: J.integer(json['cantidad']),
        before: J.integer(json['stockAnterior']),
        after: J.integer(json['stockNuevo']),
        reason: J.text(json['motivo']),
        user: J.text(json['usuarioNombre']),
        date: J.date(json['fechaMovimiento']),
      );
}

class PurchaseOrder {
  const PurchaseOrder({
    required this.id,
    required this.supplier,
    required this.status,
    required this.notes,
    required this.createdAt,
    required this.total,
    required this.ordered,
    required this.received,
  });

  final int id;
  final String supplier;
  final String status;
  final String notes;
  final DateTime? createdAt;
  final double total;
  final int ordered;
  final int received;

  double get progress => ordered == 0 ? 0 : (received / ordered).clamp(0, 1);

  factory PurchaseOrder.fromJson(Map<String, dynamic> json) => PurchaseOrder(
        id: J.integer(json['ordenCompraId']),
        supplier: J.text(json['proveedorNombre']),
        status: J.text(json['estado']),
        notes: J.text(json['notas']),
        createdAt: J.date(json['fechaCreacion']),
        total: J.decimal(json['montoTotal']),
        ordered: J.integer(json['totalOrdenado']),
        received: J.integer(json['totalRecibido']),
      );
}

class PurchaseOrderDetail {
  const PurchaseOrderDetail({
    required this.id,
    required this.supplier,
    required this.status,
    required this.notes,
    required this.createdAt,
    required this.canReceive,
    required this.lines,
  });

  final int id;
  final String supplier;
  final String status;
  final String notes;
  final DateTime? createdAt;
  final bool canReceive;
  final List<PurchaseOrderLine> lines;

  factory PurchaseOrderDetail.fromJson(Map<String, dynamic> json) => PurchaseOrderDetail(
        id: J.integer(json['ordenCompraId']),
        supplier: J.text(json['proveedorNombre']),
        status: J.text(json['estado']),
        notes: J.text(json['notas']),
        createdAt: J.date(json['fechaCreacion']),
        canReceive: J.boolean(json['admiteRecepcion']),
        lines: J.list(json['lineas'], PurchaseOrderLine.fromJson),
      );
}

class PurchaseOrderLine {
  const PurchaseOrderLine({
    required this.id,
    required this.product,
    required this.ordered,
    required this.received,
    required this.unitPrice,
  });

  final int id;
  final String product;
  final int ordered;
  final int received;
  final double unitPrice;

  int get pending => (ordered - received).clamp(0, ordered);

  factory PurchaseOrderLine.fromJson(Map<String, dynamic> json) => PurchaseOrderLine(
        id: J.integer(json['detalleOrdenCompraId']),
        product: J.text(json['productoNombre']),
        ordered: J.integer(json['cantidadOrdenada']),
        received: J.integer(json['cantidadRecibida']),
        unitPrice: J.decimal(json['precioUnitario']),
      );
}

class PurchaseSuggestion {
  const PurchaseSuggestion({
    required this.name,
    required this.stock,
    required this.minStock,
    required this.monthlyAverage,
    required this.suggested,
    required this.insufficientData,
  });

  final String name;
  final int stock;
  final int minStock;
  final double monthlyAverage;
  final int suggested;
  final bool insufficientData;

  factory PurchaseSuggestion.fromJson(Map<String, dynamic> json) => PurchaseSuggestion(
        name: J.text(json['nombre']),
        stock: J.integer(json['stockActual']),
        minStock: J.integer(json['stockMinimo']),
        monthlyAverage: J.decimal(json['promedioVentaMensual']),
        suggested: J.integer(json['cantidadSugerida']),
        insufficientData: J.boolean(json['datosInsuficientes']),
      );
}

class PickingOrder {
  const PickingOrder({
    required this.id,
    required this.client,
    required this.date,
    required this.status,
    required this.deliveryType,
    required this.address,
    required this.lines,
    required this.units,
    required this.preparedBy,
    required this.preparedAt,
    required this.routeCode,
  });

  final int id;
  final String client;
  final DateTime? date;
  final String status;
  final String deliveryType;
  final String address;
  final int lines;
  final int units;
  final String preparedBy;
  final DateTime? preparedAt;
  final String routeCode;

  factory PickingOrder.fromJson(Map<String, dynamic> json) => PickingOrder(
        id: J.integer(json['pedidoId']),
        client: J.text(json['cliente']),
        date: J.date(json['fechaPedido']),
        status: J.text(json['estado']),
        deliveryType: J.text(json['tipoEntrega']),
        address: J.text(json['direccionEntrega']),
        lines: J.integer(json['totalLineas']),
        units: J.integer(json['totalUnidades']),
        preparedBy: J.text(json['preparadoPorNombre']),
        preparedAt: J.date(json['fechaPreparacion']),
        routeCode: J.text(json['rutaCodigo']),
      );
}

// ── Acceso ───────────────────────────────────────────────────────────────────

/// Inventario, compras y preparación de pedidos.
///
/// Lecturas: directas contra la API. Escrituras de bodega (movimientos,
/// recepciones, pedidos preparados): van por la cola, porque la bodega es
/// justo donde la señal falla, y la API las acepta repetidas sin duplicar.
class WarehouseApi {
  WarehouseApi({required ApiClient api, required SyncService sync, Uuid? uuid})
      : _api = api,
        _sync = sync,
        _uuid = uuid ?? const Uuid();

  final ApiClient _api;
  final SyncService _sync;
  final Uuid _uuid;

  Future<List<Product>> products({String search = '', String filter = 'Todos'}) async {
    final data = await _api.get<List<dynamic>>('api/mobile/v1/inventory/products', query: {
      if (search.isNotEmpty) 'buscar': search,
      'filtro': filter,
    });
    return data.map((item) => Product.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<List<InventoryMovement>> movements(int productId) async {
    final data = await _api.get<List<dynamic>>('api/mobile/v1/inventory/products/$productId/movements');
    return data.map((item) => InventoryMovement.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<void> registerMovement({
    required Product product,
    required String type,
    required int quantity,
    required String reason,
  }) async {
    final syncGuid = _uuid.v4();
    final verb = switch (type) { 'Entrada' => 'Entrada', 'Salida' => 'Salida', _ => 'Ajuste a' };

    await _sync.enqueue(OutboxEntry(
      syncGuid: syncGuid,
      tipo: 'inventario_movimiento',
      endpoint: 'api/mobile/v1/inventory/movements',
      payload: {
        'productoId': product.id,
        'tipoMovimiento': type,
        'cantidad': quantity,
        'motivo': reason,
        'syncGuid': syncGuid,
      },
      descripcion: '$verb $quantity · ${product.name}',
      estado: OutboxStatus.pendiente,
      intentos: 0,
      creadoEn: DateTime.now(),
    ));
  }

  /// Activar o inactivar no va por la cola: quien lo hace necesita saber en el
  /// momento si el catálogo cambió.
  Future<Product> setActive(Product product, bool active) async {
    await _api.post<Map<String, dynamic>>(
      'api/mobile/v1/inventory/products/${product.id}/status',
      body: {'activo': active, 'syncGuid': _uuid.v4()},
    );
    return product.copyWith(active: active);
  }

  Future<List<PurchaseOrder>> purchaseOrders({String status = 'Abiertas'}) async {
    final data = await _api.get<List<dynamic>>('api/mobile/v1/inventory/purchase-orders', query: {
      if (status.isNotEmpty) 'estado': status,
    });
    return data.map((item) => PurchaseOrder.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<PurchaseOrderDetail> purchaseOrder(int id) async =>
      PurchaseOrderDetail.fromJson(await _api.get<Map<String, dynamic>>('api/mobile/v1/inventory/purchase-orders/$id'));

  Future<void> receiveLine({
    required PurchaseOrderDetail order,
    required PurchaseOrderLine line,
    required int quantity,
  }) async {
    final syncGuid = _uuid.v4();
    await _sync.enqueue(OutboxEntry(
      syncGuid: syncGuid,
      tipo: 'compra_recepcion',
      endpoint: 'api/mobile/v1/inventory/purchase-orders/${order.id}/lines/${line.id}/receive',
      payload: {'cantidad': quantity, 'syncGuid': syncGuid},
      descripcion: 'Recepción de $quantity · ${line.product} (OC #${order.id})',
      estado: OutboxStatus.pendiente,
      intentos: 0,
      creadoEn: DateTime.now(),
    ));
  }

  Future<List<PurchaseSuggestion>> suggestions() async {
    final data = await _api.get<List<dynamic>>('api/mobile/v1/inventory/purchase-suggestions');
    return data.map((item) => PurchaseSuggestion.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<List<PickingOrder>> picking({required bool prepared}) async {
    final data = await _api.get<List<dynamic>>('api/mobile/v1/orders/picking', query: {'preparados': prepared});
    return data.map((item) => PickingOrder.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<void> markPrepared({required int orderId, required String client, String notes = ''}) async {
    final syncGuid = _uuid.v4();
    await _sync.enqueue(OutboxEntry(
      syncGuid: syncGuid,
      tipo: 'pedido_preparado',
      endpoint: 'api/mobile/v1/orders/$orderId/prepared',
      payload: {'observaciones': notes, 'syncGuid': syncGuid},
      descripcion: 'Pedido #$orderId preparado · $client',
      estado: OutboxStatus.pendiente,
      intentos: 0,
      creadoEn: DateTime.now(),
    ));
  }
}

final warehouseApiProvider = Provider<WarehouseApi>(
  (ref) => WarehouseApi(
    api: ref.watch(apiClientProvider),
    sync: ref.watch(syncServiceProvider),
    uuid: ref.watch(uuidProvider),
  ),
);
