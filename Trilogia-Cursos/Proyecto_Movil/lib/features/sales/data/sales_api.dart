import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/providers.dart';
import '../../../core/sync/outbox_entry.dart';
import '../../../core/sync/sync_service.dart';
import '../../../shared/utils/formatters.dart';
import 'sales_catalog_cache.dart';

class SaleClient {
  const SaleClient(this.id, this.name, this.email, this.phone, this.address);

  final int id;
  final String name;
  final String email;
  final String phone;
  final String address;

  factory SaleClient.fromJson(Map<String, dynamic> json) => SaleClient(
        J.integer(json['clienteId']),
        J.text(json['nombre']),
        J.text(json['correo']),
        J.text(json['telefono']),
        J.text(json['direccion']),
      );
}

class SaleProduct {
  const SaleProduct(this.id, this.name, this.category, this.price, this.stock);

  final int id;
  final String name;
  final String category;
  final double price;
  final int stock;

  factory SaleProduct.fromJson(Map<String, dynamic> json) => SaleProduct(
        J.integer(json['productoId']),
        J.text(json['nombre']),
        J.text(json['categoria']),
        J.decimal(json['precio']),
        J.integer(json['stock']),
      );
}

class SellerOrder {
  const SellerOrder(this.id, this.client, this.date, this.status, this.total, this.invoiceNumber);

  final int id;
  final String client;
  final DateTime? date;
  final String status;
  final double total;
  final String invoiceNumber;

  factory SellerOrder.fromJson(Map<String, dynamic> json) => SellerOrder(
        J.integer(json['pedidoId']),
        J.text(json['cliente']),
        J.date(json['fechaPedido']),
        J.text(json['estado']),
        J.decimal(json['total']),
        J.text(json['numeroFactura']),
      );
}

/// Venta en campo.
///
/// La venta viaja por la cola: el vendedor visita clientes donde la señal va y
/// viene, y perder un pedido tomado es perder la venta. La API la acepta
/// repetida sin duplicar (el identificador llega como PedidoOfflineGuid).
///
/// Clientes y productos se guardan en el telefono cada vez que llegan completos
/// (sin filtro). Sin señal se usa esa copia y se filtra aqui mismo.
class SalesApi {
  SalesApi({required ApiClient api, required SyncService sync, SalesCatalogCache? cache, Uuid? uuid})
      : _api = api,
        _sync = sync,
        _cache = cache,
        _uuid = uuid ?? const Uuid();

  final ApiClient _api;
  final SyncService _sync;
  final SalesCatalogCache? _cache;
  final Uuid _uuid;

  static const _clientsKey = 'clientes';
  static const _productsKey = 'productos';

  /// Las mismas opciones del formulario web de venta móvil.
  static const deliveryTypes = ['Entrega por vendedor', 'Retiro en local', 'Envío a domicilio'];

  Future<List<SaleClient>> clients(String search) async {
    final data = await _listWithFallback('api/mobile/v1/sales/clients', _clientsKey, search,
        (item) => [item['nombre'], item['correo'], item['telefono']]);
    return data.map((item) => SaleClient.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<List<SaleProduct>> products(String search) async {
    final data = await _listWithFallback('api/mobile/v1/sales/products', _productsKey, search,
        (item) => [item['nombre'], item['categoria']]);
    return data.map((item) => SaleProduct.fromJson(item as Map<String, dynamic>)).toList();
  }

  Future<List<dynamic>> _listWithFallback(
    String path,
    String cacheKey,
    String search,
    List<Object?> Function(Map<String, dynamic> item) searchableFields,
  ) async {
    final cache = _cache;
    try {
      final data = await _api.get<List<dynamic>>(path, query: {
        if (search.isNotEmpty) 'buscar': search,
      });
      if (cache != null && search.isEmpty) await cache.save(cacheKey, data);
      return data;
    } on ApiException catch (error) {
      // Solo una falla de red cae a la copia local. Un 403 o una regla de
      // negocio no se esconden detras de datos viejos.
      final offline = error.failure == ApiFailure.sinConexion || error.failure == ApiFailure.tiempoAgotado;
      final saved = (offline && cache != null) ? await cache.read(cacheKey) : null;
      if (saved == null) rethrow;
      return filterCatalog(saved, search, searchableFields);
    }
  }

  /// Filtro local equivalente a la busqueda del servidor: por texto parcial,
  /// sin distinguir mayusculas.
  static List<dynamic> filterCatalog(
    List<dynamic> items,
    String search,
    List<Object?> Function(Map<String, dynamic> item) searchableFields,
  ) {
    final term = search.trim().toLowerCase();
    if (term.isEmpty) return items;
    return items.where((item) {
      final fields = searchableFields(item as Map<String, dynamic>);
      return fields.any((field) => (field?.toString() ?? '').toLowerCase().contains(term));
    }).toList();
  }

  Future<List<SellerOrder>> myOrders() async => (await _api.get<List<dynamic>>('api/mobile/v1/sales/orders'))
      .map((item) => SellerOrder.fromJson(item as Map<String, dynamic>))
      .toList();

  Future<void> submit({
    required SaleClient client,
    required String deliveryType,
    required String address,
    required String notes,
    required Map<SaleProduct, int> items,
    required bool online,
  }) async {
    final syncGuid = _uuid.v4();
    final total = items.entries.fold<double>(0, (sum, entry) => sum + entry.key.price * entry.value);

    await _sync.enqueue(OutboxEntry(
      syncGuid: syncGuid,
      tipo: 'venta',
      endpoint: 'api/mobile/v1/sales/orders',
      payload: {
        'clienteId': client.id,
        'tipoEntrega': deliveryType,
        'direccionEntrega': address,
        'observaciones': notes,
        'items': [
          for (final entry in items.entries) {'productoId': entry.key.id, 'cantidad': entry.value},
        ],
        'syncGuid': syncGuid,
        'registradoSinConexion': !online,
      },
      descripcion: 'Venta a ${client.name} · ${Fmt.money(total)}',
      estado: OutboxStatus.pendiente,
      intentos: 0,
      creadoEn: DateTime.now(),
    ));
  }
}

final salesApiProvider = Provider<SalesApi>(
  (ref) => SalesApi(
    api: ref.watch(apiClientProvider),
    sync: ref.watch(syncServiceProvider),
    cache: SqliteSalesCatalogCache(ref.watch(appDatabaseProvider)),
    uuid: ref.watch(uuidProvider),
  ),
);
