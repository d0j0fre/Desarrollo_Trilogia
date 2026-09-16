import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../features/auth/application/auth_controller.dart';
import '../features/auth/data/auth_api.dart';
import '../features/driver_routes/data/routes_local_dao.dart';
import '../features/driver_routes/data/routes_repository.dart';
import '../features/mileage/data/mileage_repository.dart';
import 'network/api_client.dart';
import 'storage/app_database.dart';
import 'storage/secure_store.dart';
import 'sync/outbox_repository.dart';
import 'sync/sync_service.dart';

/// Cableado de la aplicacion.
///
/// Todo se resuelve desde aqui para que las pantallas no construyan sus propias
/// dependencias: una pantalla que crea su cliente HTTP es una pantalla que no
/// se puede probar y que se salta el interceptor de sesion.

final uuidProvider = Provider<Uuid>((ref) => const Uuid());

final secureStoreProvider = Provider<SecureStore>((ref) => SecureStore());

/// La base se abre una vez al arrancar. Se sobrescribe en `main` con la
/// instancia ya abierta, y en las pruebas con una base en memoria.
final appDatabaseProvider = Provider<AppDatabase>(
  (ref) => throw UnimplementedError('appDatabaseProvider debe sobrescribirse'),
);

final outboxRepositoryProvider = Provider<OutboxRepository>(
  (ref) => OutboxRepository(ref.watch(appDatabaseProvider)),
);

final apiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient(
    store: ref.watch(secureStoreProvider),
    // Cuando la renovacion falla, el controlador de sesion limpia todo y el
    // router redirige al login solo. La pantalla no tiene que enterarse.
    onSessionLost: () => ref.read(authControllerProvider.notifier).sessionLost(),
  );
});

final syncServiceProvider = Provider<SyncService>((ref) {
  final service = SyncService(
    api: ref.watch(apiClientProvider),
    outbox: ref.watch(outboxRepositoryProvider),
  );
  ref.onDispose(service.dispose);
  return service;
});

/// Estado de la cola, para la franja superior y la pantalla de pendientes.
final syncStateProvider = StreamProvider<SyncState>((ref) {
  final service = ref.watch(syncServiceProvider);
  return service.stream;
});

/// Conectividad. Alimenta al servicio de sincronizacion: recuperar señal es el
/// mejor momento para vaciar la cola.
final connectivityProvider = Provider<StreamSubscription<dynamic>>((ref) {
  final sync = ref.watch(syncServiceProvider);

  final subscription = Connectivity().onConnectivityChanged.listen((results) {
    final hayConexion = results.any((result) => result != ConnectivityResult.none);
    sync.setConnectivity(hayConexion);
  });

  ref.onDispose(subscription.cancel);
  return subscription;
});

final authApiProvider = Provider<AuthApi>(
  (ref) => AuthApi(ref.watch(apiClientProvider)),
);

final routesLocalDaoProvider = Provider<RoutesLocalDao>(
  (ref) => RoutesLocalDao(ref.watch(appDatabaseProvider)),
);

final routesRepositoryProvider = Provider<RoutesRepository>(
  (ref) => RoutesRepository(
    api: ref.watch(apiClientProvider),
    local: ref.watch(routesLocalDaoProvider),
    sync: ref.watch(syncServiceProvider),
    uuid: ref.watch(uuidProvider),
  ),
);

final mileageRepositoryProvider = Provider<MileageRepository>(
  (ref) => MileageRepository(
    api: ref.watch(apiClientProvider),
    sync: ref.watch(syncServiceProvider),
    uuid: ref.watch(uuidProvider),
  ),
);
