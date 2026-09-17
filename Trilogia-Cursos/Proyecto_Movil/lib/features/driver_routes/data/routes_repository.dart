import 'package:uuid/uuid.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/sync/outbox_entry.dart';
import '../../../core/sync/sync_service.dart';
import '../../home/domain/day_summary.dart';
import '../domain/delivery.dart';
import '../domain/route.dart';
import 'routes_local_dao.dart';

/// Rutas y entregas del chofer.
///
/// Lectura: intenta la red, guarda en local, devuelve local. Si la red falla,
/// devuelve local igual. La pantalla nunca se queda vacia por falta de señal.
///
/// Escritura: aplica en local, encola, devuelve. La red ocurre despues y sola.
class RoutesRepository {
  RoutesRepository({
    required ApiClient api,
    required RoutesLocalDao local,
    required SyncService sync,
    Uuid? uuid,
  })  : _api = api,
        _local = local,
        _sync = sync,
        _uuid = uuid ?? const Uuid();

  final ApiClient _api;
  final RoutesLocalDao _local;
  final SyncService _sync;
  final Uuid _uuid;

  Future<List<DriverRoute>> routes({bool forzarRed = true}) async {
    if (forzarRed) {
      try {
        final data = await _api.get<List<dynamic>>('api/mobile/v1/driver/routes');
        final routes = data
            .map((item) => DriverRoute.fromJson(item as Map<String, dynamic>))
            .toList();
        await _local.saveRoutes(routes);
      } on ApiException {
        // Se sigue con lo que hay en el telefono. No es un error para el
        // usuario: es el modo normal de trabajar en zona sin cobertura.
      }
    }

    return _local.readRoutes();
  }

  Future<DriverRoute?> routeDetail(int rutaId, {bool forzarRed = true}) async {
    if (forzarRed) {
      try {
        final data = await _api
            .get<Map<String, dynamic>>('api/mobile/v1/driver/routes/$rutaId');
        await _local.saveRouteDetail(DriverRoute.fromJson(data));
      } on ApiException catch (error) {
        // Un 404 aqui significa que la ruta dejo de ser suya: se la
        // reasignaron o se cancelo. Se propaga para que la pantalla lo diga.
        if (error.failure == ApiFailure.noEncontrado) rethrow;
      }
    }

    return _local.readRouteDetail(rutaId);
  }

  /// Marca una entrega.
  ///
  /// El orden importa y es intencional: primero local, luego cola. Si se
  /// invirtiera y el proceso muriera en medio, quedaria una accion encolada que
  /// la pantalla no refleja, y el chofer la volveria a marcar.
  Future<void> marcarEntrega({
    required int rutaPedidoId,
    required int pedidoId,
    required String estado,
    String? motivoFallo,
  }) async {
    final syncGuid = _uuid.v4();

    await _local.applyLocalStatus(
      rutaPedidoId: rutaPedidoId,
      estado: estado,
      motivoFallo: motivoFallo ?? '',
    );

    await _sync.enqueue(
      OutboxEntry(
        syncGuid: syncGuid,
        tipo: 'entrega',
        endpoint: 'api/mobile/v1/driver/deliveries/$rutaPedidoId/status',
        payload: {
          'estado': estado,
          'syncGuid': syncGuid,
          if (motivoFallo != null && motivoFallo.trim().isNotEmpty)
            'motivoFallo': motivoFallo.trim(),
        },
        descripcion: estado == DeliveryStatus.entregado
            ? 'Pedido #$pedidoId entregado'
            : 'Pedido #$pedidoId no entregado',
        estado: OutboxStatus.pendiente,
        intentos: 0,
        creadoEn: DateTime.now(),
      ),
    );
  }

  Future<DaySummary> daySummary() async {
    final data = await _api.get<Map<String, dynamic>>('api/mobile/v1/driver/summary');
    return DaySummary.fromJson(data);
  }
}
