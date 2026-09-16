import 'dart:async';

import 'package:flutter/foundation.dart';

import '../config/app_config.dart';
import '../network/api_client.dart';
import '../network/api_exception.dart';
import 'outbox_entry.dart';
import 'outbox_repository.dart';

@immutable
class SyncState {
  const SyncState({
    this.pendientes = 0,
    this.conflictos = 0,
    this.enviando = false,
    this.hayConexion = true,
    this.ultimaSincronizacion,
  });

  final int pendientes;
  final int conflictos;
  final bool enviando;
  final bool hayConexion;
  final DateTime? ultimaSincronizacion;

  bool get todoAlDia => pendientes == 0 && conflictos == 0;

  SyncState copyWith({
    int? pendientes,
    int? conflictos,
    bool? enviando,
    bool? hayConexion,
    DateTime? ultimaSincronizacion,
  }) =>
      SyncState(
        pendientes: pendientes ?? this.pendientes,
        conflictos: conflictos ?? this.conflictos,
        enviando: enviando ?? this.enviando,
        hayConexion: hayConexion ?? this.hayConexion,
        ultimaSincronizacion: ultimaSincronizacion ?? this.ultimaSincronizacion,
      );
}

/// Vacia la cola contra el servidor.
///
/// Reglas, y el porque de cada una:
///
/// - **Una a la vez, en orden de creacion.** Marcar una entrega y despues
///   cerrar la jornada tiene un orden que importa.
/// - **Exito o duplicado confirman igual.** Que el servidor responda
///   `duplicado: true` significa que la accion ya se aplico: exactamente lo que
///   se queria.
/// - **Una regla de negocio no se reintenta.** Si el pedido se cancelo desde la
///   web mientras el chofer estaba sin señal, insistir no lo va a arreglar. Se
///   marca conflicto y lo decide una persona.
/// - **Una falla de red si se reintenta**, con espera creciente.
class SyncService {
  SyncService({
    required ApiClient api,
    required OutboxRepository outbox,
  })  : _api = api,
        _outbox = outbox;

  final ApiClient _api;
  final OutboxRepository _outbox;

  final _controller = StreamController<SyncState>.broadcast();
  Stream<SyncState> get stream => _controller.stream;

  SyncState _state = const SyncState();
  SyncState get state => _state;

  Timer? _timer;
  bool _corriendo = false;

  void start() {
    _timer ??= Timer.periodic(AppConfig.syncInterval, (_) => flush());
    unawaited(refreshCounters());
  }

  void stop() {
    _timer?.cancel();
    _timer = null;
  }

  void setConnectivity(bool hayConexion) {
    _emit(_state.copyWith(hayConexion: hayConexion));
    // Recuperar señal es el mejor momento para vaciar la cola: es cuando el
    // chofer acaba de salir de una zona muerta con entregas acumuladas.
    if (hayConexion) unawaited(flush());
  }

  Future<void> refreshCounters() async {
    _emit(_state.copyWith(
      pendientes: await _outbox.pendientesCount(),
      conflictos: await _outbox.conflictosCount(),
    ));
  }

  /// Encola y dispara un envio. Devuelve de inmediato: la interfaz no espera a
  /// la red para mostrar la entrega como marcada.
  Future<void> enqueue(OutboxEntry entry) async {
    await _outbox.enqueue(entry);
    await refreshCounters();
    unawaited(flush());
  }

  Future<void> flush() async {
    if (_corriendo || !_state.hayConexion) return;

    _corriendo = true;
    _emit(_state.copyWith(enviando: true));

    try {
      final ahora = DateTime.now();
      final pendientes = await _outbox.pendientes();

      for (final entry in pendientes) {
        if (!entry.listaParaEnviar(ahora)) continue;
        final continuar = await _send(entry);
        // Si se cayo la red, no tiene sentido seguir con el resto de la cola.
        if (!continuar) break;
      }
    } finally {
      _corriendo = false;
      await refreshCounters();
      _emit(_state.copyWith(
        enviando: false,
        ultimaSincronizacion: DateTime.now(),
      ));
    }
  }

  /// Devuelve `false` cuando conviene detener el vaciado (red caida).
  Future<bool> _send(OutboxEntry entry) async {
    try {
      await _api.post<Map<String, dynamic>>(
        entry.endpoint,
        body: entry.payload,
      );

      // 2xx, incluyendo la respuesta con `duplicado: true`. La accion esta
      // aplicada del lado del servidor, que es lo unico que importa.
      await _outbox.remove(entry.syncGuid);
      return true;
    } on ApiException catch (error) {
      if (error.esConflicto) {
        await _outbox.update(entry.copyWith(
          estado: OutboxStatus.conflicto,
          ultimoError: error.message,
          limpiarProximoIntento: true,
        ));
        return true;
      }

      if (error.failure == ApiFailure.sesionExpirada ||
          error.failure == ApiFailure.sinPermiso) {
        // No se reintenta: no depende de la red. La sesion ya se cerro sola
        // desde el interceptor, o el permiso se revoco.
        await _outbox.update(entry.copyWith(
          estado: OutboxStatus.conflicto,
          ultimoError: error.message,
          limpiarProximoIntento: true,
        ));
        return true;
      }

      final intentos = entry.intentos + 1;

      if (intentos >= OutboxEntry.maxIntentos) {
        // Se rinde y lo muestra. Una accion reintentandose en silencio para
        // siempre es peor que una que el chofer puede ver y resolver.
        await _outbox.update(entry.copyWith(
          estado: OutboxStatus.conflicto,
          intentos: intentos,
          ultimoError:
              'No se pudo enviar después de $intentos intentos. ${error.message}',
          limpiarProximoIntento: true,
        ));
        return true;
      }

      await _outbox.update(entry.copyWith(
        intentos: intentos,
        proximoIntento: DateTime.now().add(OutboxEntry.esperaPara(intentos)),
        ultimoError: error.message,
      ));

      return error.failure != ApiFailure.sinConexion;
    }
  }

  Future<void> descartar(String syncGuid) async {
    await _outbox.remove(syncGuid);
    await refreshCounters();
  }

  Future<void> reintentar(String syncGuid) async {
    await _outbox.reintentar(syncGuid);
    await refreshCounters();
    unawaited(flush());
  }

  void _emit(SyncState next) {
    _state = next;
    if (!_controller.isClosed) _controller.add(next);
  }

  void dispose() {
    stop();
    _controller.close();
  }
}
