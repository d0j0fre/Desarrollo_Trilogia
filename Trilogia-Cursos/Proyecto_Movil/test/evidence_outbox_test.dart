import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/core/network/api_client.dart';
import 'package:proyecto_movil/core/network/api_exception.dart';
import 'package:proyecto_movil/core/storage/evidence_file_store.dart';
import 'package:proyecto_movil/core/sync/outbox_entry.dart';
import 'package:proyecto_movil/core/sync/outbox_repository.dart';
import 'package:proyecto_movil/core/sync/sync_service.dart';
import 'package:proyecto_movil/features/driver_routes/data/routes_local_dao.dart';
import 'package:proyecto_movil/features/driver_routes/data/routes_repository.dart';
import 'package:proyecto_movil/features/driver_routes/domain/delivery.dart';
import 'package:proyecto_movil/features/driver_routes/domain/evidence_draft.dart';

/// G3-04: la foto y la firma de la entrega viajan por la misma cola «Por
/// enviar» que el estado: primero el estado, despues los archivos, y sin perder
/// nada si no hay señal o se cierra la aplicacion.
void main() {
  late Directory temp;
  late EvidenceFileStore files;
  late _MemoryOutbox outbox;
  late _FakeApi api;
  late SyncService sync;
  late RoutesRepository repo;

  // Un JPG y un PNG minimos: el contenido real lo valida el servidor.
  final jpg = Uint8List.fromList([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8]);
  final png = Uint8List.fromList([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2]);

  setUp(() async {
    temp = await Directory.systemTemp.createTemp('evidencia_test_');
    files = EvidenceFileStore(baseDirectory: () async => temp);
    outbox = _MemoryOutbox();
    api = _FakeApi();
    sync = SyncService(api: api, outbox: outbox, files: files);
    repo = RoutesRepository(api: api, local: _NoLocal(), sync: sync);
  });

  tearDown(() async {
    sync.dispose();
    if (await temp.exists()) await temp.delete(recursive: true);
  });

  /// Deja una foto y una firma capturadas, ya en el almacenamiento de la app.
  Future<List<EvidenceDraft>> capturar() async {
    final origen = File('${temp.path}${Platform.pathSeparator}camara.jpg')..writeAsBytesSync(jpg);
    final fotoPath = await files.importar(origen: origen.path, syncGuid: 'foto-1');
    final firmaPath = await files.guardarBytes(bytes: png, syncGuid: 'firma-1');
    return [
      EvidenceDraft(tipo: EvidenceType.foto, syncGuid: 'foto-1', path: fotoPath!),
      EvidenceDraft(tipo: EvidenceType.firma, syncGuid: 'firma-1', path: firmaPath),
    ];
  }

  /// Encolar dispara un envio en segundo plano; se espera a que termine.
  Future<void> esperarCola() async {
    await sync.flush();
    while (sync.state.enviando) {
      await Future<void>.delayed(const Duration(milliseconds: 5));
    }
  }

  /// Vuelve la señal. Las esperas por reintento (1 s, 2 s, 4 s…) se saltan: en la
  /// aplicacion real simplemente pasa el tiempo.
  Future<void> volverLaSenal() async {
    api.sinConexion = false;
    for (final entrada in await outbox.todas()) {
      await outbox.update(entrada.copyWith(limpiarProximoIntento: true));
    }
  }

  Future<void> entregarConEvidencia() async {
    // Sin señal desde el principio: nada sale hasta que el servidor responda.
    api.sinConexion = true;
    await repo.marcarEntrega(
      rutaPedidoId: 14,
      pedidoId: 54,
      estado: DeliveryStatus.entregado,
      evidencias: await capturar(),
    );
    await esperarCola();
  }

  group('Cola', () {
    test('el estado entra primero y la foto y la firma esperan a ese estado', () async {
      api.sinConexion = true;
      await repo.marcarEntrega(
        rutaPedidoId: 14,
        pedidoId: 54,
        estado: DeliveryStatus.entregado,
        evidencias: await capturar(),
      );

      final entradas = await outbox.todas();
      expect(entradas.map((e) => e.tipo), ['entrega', 'evidencia', 'evidencia']);
      expect(entradas[0].endpoint, 'api/mobile/v1/driver/deliveries/14/status');
      expect(entradas[1].endpoint, 'api/mobile/v1/driver/deliveries/14/evidence');
      expect(entradas[1].payload['tipo'], 'Foto');
      expect(entradas[2].payload['tipo'], 'Firma');
      // Las dos dependen del estado: es lo que garantiza el orden.
      expect(entradas[1].dependeDe, entradas[0].syncGuid);
      expect(entradas[2].dependeDe, entradas[0].syncGuid);
      // Cada archivo lleva su propio identificador, igual en cada reintento.
      expect(entradas[1].syncGuid, 'foto-1');
      expect(entradas[2].syncGuid, 'firma-1');
    });

    test('sin evidencia solo se encola el estado, como antes', () async {
      api.sinConexion = true;
      await repo.marcarEntrega(rutaPedidoId: 14, pedidoId: 54, estado: DeliveryStatus.entregado);

      expect((await outbox.todas()).map((e) => e.tipo), ['entrega']);
    });

    test('sin señal no sale nada y los archivos siguen en el teléfono', () async {
      await entregarConEvidencia();

      expect(await outbox.pendientesCount(), 3);
      expect(await outbox.conflictosCount(), 0);
      for (final entrada in (await outbox.todas()).where((e) => e.esEvidencia)) {
        expect(await files.existe(entrada.archivo!), isTrue, reason: 'el archivo no puede perderse sin señal');
      }
    });

    test('al volver la señal sale primero el estado y despues los archivos, y se limpian', () async {
      await entregarConEvidencia();
      final rutaFoto = (await outbox.todas())[1].archivo!;
      final rutaFirma = (await outbox.todas())[2].archivo!;

      await volverLaSenal();
      await esperarCola();

      expect(api.llamadas.map((c) => c.path), [
        'api/mobile/v1/driver/deliveries/14/status',
        'api/mobile/v1/driver/deliveries/14/evidence',
        'api/mobile/v1/driver/deliveries/14/evidence',
      ]);
      expect(await outbox.todas(), isEmpty);
      expect(await files.existe(rutaFoto), isFalse);
      expect(await files.existe(rutaFirma), isFalse);
    });

    test('los archivos viajan como multipart con tipo, syncGuid y la imagen', () async {
      api.sinConexion = false;
      await repo.marcarEntrega(
        rutaPedidoId: 14,
        pedidoId: 54,
        estado: DeliveryStatus.entregado,
        evidencias: await capturar(),
      );
      await esperarCola();

      final foto = api.llamadas.where((c) => c.path.endsWith('/evidence')).first;
      final form = foto.body as FormData;
      expect(Map.fromEntries(form.fields), {'tipo': 'Foto', 'syncGuid': 'foto-1'});
      expect(form.files.single.key, 'archivo');
      expect(form.files.single.value.filename, 'evidencia.jpg');
      expect(form.files.single.value.contentType.toString(), 'image/jpeg');

      final firma = api.llamadas.where((c) => c.path.endsWith('/evidence')).last.body as FormData;
      expect(firma.files.single.value.filename, 'evidencia.png');
      expect(firma.files.single.value.contentType.toString(), 'image/png');
    });

    test('al recuperar la señal salen enseguida, sin esperar el reintento programado', () async {
      await entregarConEvidencia();
      // Tras varios fallos el estado quedo programado para dentro de minutos.
      final estado = (await outbox.todas()).first;
      await outbox.update(estado.copyWith(intentos: 5, proximoIntento: DateTime.now().add(const Duration(minutes: 5))));

      api.sinConexion = false;
      sync.setConnectivity(true);
      while (sync.state.enviando) {
        await Future<void>.delayed(const Duration(milliseconds: 5));
      }
      await esperarCola();

      expect(api.llamadas.map((c) => c.path.split('/').last), ['status', 'evidence', 'evidence']);
      expect(await outbox.todas(), isEmpty);
    });

    test('si el estado se está reintentando, la evidencia sigue esperando', () async {
      await entregarConEvidencia();
      final estado = (await outbox.todas()).first;
      // El estado fallo por red y quedo con espera: no esta «listo» todavia.
      await outbox.update(estado.copyWith(proximoIntento: DateTime.now().add(const Duration(minutes: 5))));

      api.sinConexion = false;
      await esperarCola();

      expect(api.llamadas, isEmpty, reason: 'ni el estado ni la evidencia deben salir');
      expect(await outbox.pendientesCount(), 3);
    });

    test('un conflicto en el estado deja la evidencia retenida, sin enviarla', () async {
      await entregarConEvidencia();
      await volverLaSenal();
      api.responder = (path) => path.endsWith('/status')
          ? ApiException(ApiFailure.reglaDeNegocio, 'El pedido se canceló.', statusCode: 422)
          : null;

      await esperarCola();

      expect(api.llamadas.map((c) => c.path), ['api/mobile/v1/driver/deliveries/14/status']);
      expect((await outbox.conflictos()).single.tipo, 'entrega');
      expect(await outbox.pendientesCount(), 2, reason: 'foto y firma esperan a que alguien decida');
    });

    test('descartar el estado descarta tambien sus archivos', () async {
      await entregarConEvidencia();
      final todas = await outbox.todas();
      final rutas = todas.where((e) => e.esEvidencia).map((e) => e.archivo!).toList();

      await sync.descartar(todas.first.syncGuid);

      expect(await outbox.todas(), isEmpty);
      for (final ruta in rutas) {
        expect(await files.existe(ruta), isFalse);
      }
    });

    test('una evidencia que el servidor rechaza queda como conflicto y conserva el archivo', () async {
      api.sinConexion = false;
      api.responder = (path) => path.endsWith('/evidence')
          ? ApiException(ApiFailure.reglaDeNegocio, 'Esa ruta ya no admite evidencia.', statusCode: 422)
          : null;
      await repo.marcarEntrega(
        rutaPedidoId: 14,
        pedidoId: 54,
        estado: DeliveryStatus.entregado,
        evidencias: await capturar(),
      );
      await esperarCola();

      final conflictos = await outbox.conflictos();
      expect(conflictos, hasLength(2));
      expect(conflictos.first.ultimoError, 'Esa ruta ya no admite evidencia.');
      expect(await files.existe(conflictos.first.archivo!), isTrue,
          reason: 'el chofer puede reintentar o descartar desde «Por enviar»');
    });

    test('una falla del servidor se reintenta con espera y no pierde el archivo', () async {
      api.sinConexion = false;
      api.responder = (path) => path.endsWith('/evidence')
          ? ApiException(ApiFailure.servidor, 'El servidor no está disponible.', statusCode: 503)
          : null;
      await repo.marcarEntrega(
        rutaPedidoId: 14,
        pedidoId: 54,
        estado: DeliveryStatus.entregado,
        evidencias: await capturar(),
      );
      await esperarCola();

      final foto = (await outbox.todas()).firstWhere((e) => e.syncGuid == 'foto-1');
      expect(foto.estado, OutboxStatus.pendiente);
      expect(foto.intentos, 1);
      expect(foto.proximoIntento, isNotNull);
      expect(await files.existe(foto.archivo!), isTrue);
    });

    test('un 400/413 del servidor (archivo demasiado grande) es conflicto, no se reintenta 10 veces', () async {
      api.sinConexion = false;
      api.responder = (path) => path.endsWith('/evidence')
          ? ApiException(ApiFailure.desconocido, 'No fue posible completar la operación.', statusCode: 400)
          : null;
      await repo.marcarEntrega(
        rutaPedidoId: 14,
        pedidoId: 54,
        estado: DeliveryStatus.entregado,
        evidencias: await capturar(),
      );
      await esperarCola();

      final conflictos = await outbox.conflictos();
      expect(conflictos.map((e) => e.syncGuid), containsAll(['foto-1', 'firma-1']));
      expect(conflictos.every((e) => e.intentos == 0), isTrue);
    });

    test('si el archivo ya no esta en el teléfono se avisa, no se reintenta en silencio', () async {
      await entregarConEvidencia(); // sin señal: todo queda en la cola
      await files.borrar((await outbox.todas())[1].archivo);

      await volverLaSenal();
      await esperarCola();

      final conflicto = (await outbox.conflictos()).single;
      expect(conflicto.syncGuid, 'foto-1');
      expect(conflicto.ultimoError, contains('ya no está en el teléfono'));
      // La firma, que si esta, salio igual.
      expect(api.llamadas.where((c) => c.path.endsWith('/evidence')), hasLength(1));
    });

    test('el reenvío tras un corte usa el mismo syncGuid (el servidor lo reconoce como duplicado)', () async {
      await entregarConEvidencia();
      await volverLaSenal();
      // Primer intento: la subida llega al servidor pero la respuesta se pierde.
      var intento = 0;
      api.responder = (path) => path.endsWith('/evidence') && intento++ == 0
          ? ApiException(ApiFailure.tiempoAgotado, 'El servidor está tardando.')
          : null;

      await esperarCola();
      for (final e in await outbox.todas()) {
        await outbox.update(e.copyWith(limpiarProximoIntento: true));
      }
      await esperarCola();

      final foto = api.llamadas.where((c) => c.path.endsWith('/evidence')).map((c) => Map.fromEntries((c.body as FormData).fields)['syncGuid']);
      expect(foto.where((guid) => guid == 'foto-1'), hasLength(2));
      expect(await outbox.todas(), isEmpty);
    });
  });

  group('Archivos de evidencia', () {
    test('guarda la foto en el almacenamiento propio, con el syncGuid como nombre', () async {
      final origen = File('${temp.path}${Platform.pathSeparator}cache.JPEG')..writeAsBytesSync(jpg);

      final guardada = await files.importar(origen: origen.path, syncGuid: 'abc');

      expect(guardada, isNotNull);
      expect(guardada!.endsWith('abc.jpg'), isTrue);
      expect(File(guardada).readAsBytesSync(), jpg);
      expect(guardada, isNot(origen.path));
    });

    test('rechaza formatos que el servidor no acepta (HEIC, GIF)', () async {
      for (final nombre in ['foto.heic', 'foto.gif', 'foto']) {
        final origen = File('${temp.path}${Platform.pathSeparator}$nombre')..writeAsBytesSync(jpg);
        expect(await files.importar(origen: origen.path, syncGuid: 'x'), isNull, reason: nombre);
      }
    });

    test('tipoPara reconoce JPG, PNG y WEBP', () {
      expect(EvidenceFileStore.tipoPara('a/b.JPG')!.contentType, 'image/jpeg');
      expect(EvidenceFileStore.tipoPara('a/b.jpeg')!.extension, '.jpg');
      expect(EvidenceFileStore.tipoPara('a/b.png')!.contentType, 'image/png');
      expect(EvidenceFileStore.tipoPara('a/b.webp')!.contentType, 'image/webp');
      expect(EvidenceFileStore.tipoPara('a/b.bmp'), isNull);
    });

    test('limpiarHuerfanos borra solo lo viejo que la cola no referencia', () async {
      final referenciado = await files.guardarBytes(bytes: png, syncGuid: 'a');
      final huerfanoViejo = await files.guardarBytes(bytes: png, syncGuid: 'b');
      final huerfanoReciente = await files.guardarBytes(bytes: png, syncGuid: 'c');
      File(referenciado).setLastModifiedSync(DateTime.now().subtract(const Duration(days: 2)));
      File(huerfanoViejo).setLastModifiedSync(DateTime.now().subtract(const Duration(days: 2)));

      final borrados = await files.limpiarHuerfanos({referenciado});

      expect(borrados, 1);
      expect(File(referenciado).existsSync(), isTrue);
      expect(File(huerfanoViejo).existsSync(), isFalse);
      expect(File(huerfanoReciente).existsSync(), isTrue, reason: 'puede estar capturandose ahora mismo');
    });

    test('borrarTodo vacia las evidencias (cierre de sesion)', () async {
      final a = await files.guardarBytes(bytes: png, syncGuid: 'a');
      final b = await files.guardarBytes(bytes: png, syncGuid: 'b');

      await files.borrarTodo();

      expect(File(a).existsSync(), isFalse);
      expect(File(b).existsSync(), isFalse);
    });
  });
}

/// Cola en memoria: la real usa SQLite, que no corre en las pruebas de widgets.
class _MemoryOutbox implements OutboxRepository {
  final _filas = <String, OutboxEntry>{};

  @override
  Future<void> enqueue(OutboxEntry entry) async => _filas.putIfAbsent(entry.syncGuid, () => entry);

  @override
  Future<List<OutboxEntry>> todas() async => _filas.values.toList();

  @override
  Future<List<OutboxEntry>> pendientes() async =>
      _filas.values.where((e) => e.estado == OutboxStatus.pendiente).toList();

  @override
  Future<List<OutboxEntry>> conflictos() async =>
      _filas.values.where((e) => e.estado == OutboxStatus.conflicto).toList();

  @override
  Future<int> pendientesCount() async => (await pendientes()).length;

  @override
  Future<int> conflictosCount() async => (await conflictos()).length;

  @override
  Future<void> update(OutboxEntry entry) async => _filas[entry.syncGuid] = entry;

  @override
  Future<void> remove(String syncGuid) async => _filas.remove(syncGuid);

  @override
  Future<OutboxEntry?> porGuid(String syncGuid) async => _filas[syncGuid];

  @override
  Future<bool> existe(String syncGuid) async => _filas.containsKey(syncGuid);

  @override
  Future<List<OutboxEntry>> dependientesDe(String syncGuid) async =>
      _filas.values.where((e) => e.dependeDe == syncGuid).toList();

  @override
  Future<void> reiniciarEsperas() async {
    for (final e in _filas.values.where((e) => e.estado == OutboxStatus.pendiente).toList()) {
      _filas[e.syncGuid] = e.copyWith(limpiarProximoIntento: true);
    }
  }

  @override
  Future<void> reintentar(String syncGuid) async {
    final e = _filas[syncGuid];
    if (e != null) {
      _filas[syncGuid] = e.copyWith(estado: OutboxStatus.pendiente, intentos: 0, limpiarProximoIntento: true);
    }
  }
}

class _Llamada {
  _Llamada(this.path, this.body);
  final String path;
  final Object? body;
}

/// Servidor falso: registra cada POST y responde segun el caso.
class _FakeApi implements ApiClient {
  final llamadas = <_Llamada>[];
  bool sinConexion = false;

  /// Devuelve la excepcion que debe lanzarse para esa ruta, o `null` si responde 200.
  ApiException? Function(String path)? responder;

  @override
  Future<T> post<T>(
    String path, {
    Object? body,
    bool anonymous = false,
    Duration? receiveTimeout,
    Duration? sendTimeout,
  }) async {
    if (sinConexion) {
      throw ApiException(ApiFailure.sinConexion, 'Sin conexión. Se guardó y se enviará al recuperar señal.');
    }
    llamadas.add(_Llamada(path, body));
    final falla = responder?.call(path);
    if (falla != null) throw falla;
    return <String, dynamic>{'duplicado': false} as T;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _NoLocal implements RoutesLocalDao {
  @override
  Future<void> applyLocalStatus({
    required int rutaPedidoId,
    required String estado,
    String motivoFallo = '',
  }) async {}

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
