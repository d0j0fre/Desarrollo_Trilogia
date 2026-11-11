import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../config/app_config.dart';
import '../network/api_client.dart';
import '../network/api_exception.dart';
import '../storage/evidence_file_store.dart';
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
/// - **La evidencia (foto, firma) espera al estado de su entrega.** Sale despues
///   de la entrada de la que depende (`dependeDe`), aunque esa este reintentandose
///   por falta de señal: primero el estado, despues los archivos.
class SyncService {
  SyncService({
    required ApiClient api,
    required OutboxRepository outbox,
    EvidenceFileStore? files,
  })  : _api = api,
        _outbox = outbox,
        _files = files;

  final ApiClient _api;
  final OutboxRepository _outbox;
  final EvidenceFileStore? _files;

  final _controller = StreamController<SyncState>.broadcast();
  Stream<SyncState> get stream => _controller.stream;

  SyncState _state = const SyncState();
  SyncState get state => _state;

  Timer? _timer;
  bool _corriendo = false;

  void start() {
    _timer ??= Timer.periodic(AppConfig.syncInterval, (_) => flush());
    unawaited(refreshCounters());
    unawaited(_limpiarArchivosHuerfanos());
  }

  void stop() {
    _timer?.cancel();
    _timer = null;
  }

  void setConnectivity(bool hayConexion) {
    _emit(_state.copyWith(hayConexion: hayConexion));
    // Recuperar señal es el mejor momento para vaciar la cola: es cuando el
    // chofer acaba de salir de una zona muerta con entregas acumuladas. Las
    // esperas de reintento acumuladas sin señal se quitan: si no, lo ultimo que
    // fallo esperaria hasta 5 minutos con la red ya disponible.
    if (hayConexion) unawaited(_vaciarTrasRecuperarSenal());
  }

  Future<void> _vaciarTrasRecuperarSenal() async {
    await _outbox.reiniciarEsperas();
    await flush();
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

  /// Hay un vaciado en curso y alguien pidio otro: se hace una vuelta mas al
  /// terminar. Sin esto, la foto encolada justo despues del estado esperaba hasta
  /// la siguiente revision periodica.
  bool _repetir = false;

  Future<void> flush() async {
    if (!_state.hayConexion) return;
    if (_corriendo) {
      _repetir = true;
      return;
    }

    _corriendo = true;
    _emit(_state.copyWith(enviando: true));

    try {
      do {
        _repetir = false;
        final ahora = DateTime.now();
        final pendientes = await _outbox.pendientes();

        for (final entry in pendientes) {
          if (!entry.listaParaEnviar(ahora)) continue;
          if (await _esperaOtraAccion(entry)) continue;
          final continuar = await _send(entry);
          // Si se cayo la red, no tiene sentido seguir con el resto de la cola.
          if (!continuar) break;
        }
      } while (_repetir && _state.hayConexion);
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
      if (entry.esEvidencia) {
        if (!await _archivoDisponible(entry)) return true;
        await _api.post<Map<String, dynamic>>(
          entry.endpoint,
          body: await _formularioDe(entry),
          sendTimeout: _tiempoSubida,
          receiveTimeout: _tiempoSubida,
        );
      } else {
        await _api.post<Map<String, dynamic>>(
          entry.endpoint,
          body: entry.payload,
        );
      }

      // 2xx, incluyendo la respuesta con `duplicado: true`. La accion esta
      // aplicada del lado del servidor, que es lo unico que importa.
      await _outbox.remove(entry.syncGuid);
      await _borrarArchivo(entry);
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

      // Un 4xx sin forma de regla de negocio (400, 413, 415: el servidor no aceptó el
      // archivo o la solicitud) tampoco se arregla reintentando lo mismo.
      final rechazada = error.failure == ApiFailure.desconocido &&
          (error.statusCode ?? 0) >= 400 &&
          (error.statusCode ?? 0) < 500;

      if (rechazada ||
          error.failure == ApiFailure.sesionExpirada ||
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
    // Lo que dependia de esta accion (la foto y la firma de una entrega) no tiene
    // sentido sin ella: se descarta junto, con sus archivos.
    for (final dependiente in await _outbox.dependientesDe(syncGuid)) {
      await _outbox.remove(dependiente.syncGuid);
      await _borrarArchivo(dependiente);
    }

    final entry = await _outbox.porGuid(syncGuid);
    await _outbox.remove(syncGuid);
    if (entry != null) await _borrarArchivo(entry);
    await refreshCounters();
  }

  Future<void> reintentar(String syncGuid) async {
    await _outbox.reintentar(syncGuid);
    await refreshCounters();
    unawaited(flush());
  }

  static const _tiempoSubida = Duration(minutes: 2);

  /// La evidencia espera mientras la accion de la que depende siga en la cola,
  /// pendiente o con conflicto: no se sube la foto de una entrega que el
  /// servidor todavia no registro.
  Future<bool> _esperaOtraAccion(OutboxEntry entry) async {
    final previa = entry.dependeDe;
    return previa != null && await _outbox.existe(previa);
  }

  /// Si el archivo ya no esta en el telefono no se puede subir ni reintentar:
  /// queda como conflicto para que el chofer lo vea, en vez de fallar en silencio.
  Future<bool> _archivoDisponible(OutboxEntry entry) async {
    final path = entry.archivo;
    if (path != null && await (_files?.existe(path) ?? Future.value(true))) {
      return true;
    }

    await _outbox.update(entry.copyWith(
      estado: OutboxStatus.conflicto,
      ultimoError: 'El archivo ya no está en el teléfono. Volvé a tomarlo desde la entrega.',
      limpiarProximoIntento: true,
    ));
    return false;
  }

  /// Un FormData se lee una sola vez, asi que se arma de nuevo en cada intento.
  Future<FormData> _formularioDe(OutboxEntry entry) async {
    final path = entry.archivo!;
    final tipo = EvidenceFileStore.tipoPara(path)!;
    final observaciones = entry.payload['observaciones'] as String?;

    return FormData.fromMap({
      'tipo': entry.payload['tipo'],
      'syncGuid': entry.syncGuid,
      if (observaciones != null && observaciones.trim().isNotEmpty)
        'observaciones': observaciones.trim(),
      'archivo': await MultipartFile.fromFile(
        path,
        filename: 'evidencia${tipo.extension}',
        contentType: DioMediaType.parse(tipo.contentType),
      ),
    });
  }

  Future<void> _borrarArchivo(OutboxEntry entry) async {
    if (!entry.esEvidencia) return;
    try {
      await _files?.borrar(entry.archivo);
    } on Object {
      // Un archivo que no se pudo borrar no debe frenar la cola.
    }
  }

  Future<void> _limpiarArchivosHuerfanos() async {
    final files = _files;
    if (files == null) return;
    try {
      final enCola = (await _outbox.todas())
          .map((entry) => entry.archivo)
          .whereType<String>()
          .toSet();
      await files.limpiarHuerfanos(enCola);
    } on Object {
      // Es limpieza: si falla, se intenta en el proximo arranque.
    }
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
