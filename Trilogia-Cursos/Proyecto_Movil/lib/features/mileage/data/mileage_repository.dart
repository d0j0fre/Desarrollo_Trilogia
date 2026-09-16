import 'package:uuid/uuid.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/sync/outbox_entry.dart';
import '../../../core/sync/sync_service.dart';
import '../domain/mileage.dart';

/// Jornada de kilometraje.
///
/// Abrir y cerrar jornada tambien pasan por la cola: el chofer arranca a las
/// seis de la mañana desde la bodega, que muchas veces es justo donde no hay
/// señal.
class MileageRepository {
  MileageRepository({
    required ApiClient api,
    required SyncService sync,
    Uuid? uuid,
  })  : _api = api,
        _sync = sync,
        _uuid = uuid ?? const Uuid();

  final ApiClient _api;
  final SyncService _sync;
  final Uuid _uuid;

  Future<List<DriverVehicle>> vehicles() async {
    final data = await _api.get<List<dynamic>>('api/mobile/v1/driver/vehicles');
    return data
        .map((item) => DriverVehicle.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  /// Jornada abierta, o `null` si no hay ninguna. `null` no es un error: es el
  /// estado de quien todavia no sale a ruta.
  Future<OpenShift?> openShift() async {
    try {
      final data =
          await _api.get<Map<String, dynamic>?>('api/mobile/v1/driver/mileage/open');
      if (data == null || data.isEmpty) return null;
      return OpenShift.fromJson(data);
    } on ApiException {
      return null;
    }
  }

  Future<void> abrirJornada({
    required int vehiculoId,
    required String placa,
    required int kmInicial,
    String? observaciones,
  }) async {
    final syncGuid = _uuid.v4();

    await _sync.enqueue(
      OutboxEntry(
        syncGuid: syncGuid,
        tipo: 'jornada_abrir',
        endpoint: 'api/mobile/v1/driver/mileage/open',
        payload: {
          'vehiculoId': vehiculoId,
          'kmInicial': kmInicial,
          'syncGuid': syncGuid,
          if (observaciones != null && observaciones.trim().isNotEmpty)
            'observaciones': observaciones.trim(),
        },
        descripcion: 'Jornada abierta en $placa con $kmInicial km',
        estado: OutboxStatus.pendiente,
        intentos: 0,
        creadoEn: DateTime.now(),
      ),
    );
  }

  Future<void> cerrarJornada({
    required int kilometrajeId,
    required String placa,
    required int kmFinal,
  }) async {
    final syncGuid = _uuid.v4();

    await _sync.enqueue(
      OutboxEntry(
        syncGuid: syncGuid,
        tipo: 'jornada_cerrar',
        endpoint: 'api/mobile/v1/driver/mileage/close',
        payload: {
          'kilometrajeId': kilometrajeId,
          'kmFinal': kmFinal,
          'syncGuid': syncGuid,
        },
        descripcion: 'Jornada cerrada en $placa con $kmFinal km',
        estado: OutboxStatus.pendiente,
        intentos: 0,
        creadoEn: DateTime.now(),
      ),
    );
  }
}
