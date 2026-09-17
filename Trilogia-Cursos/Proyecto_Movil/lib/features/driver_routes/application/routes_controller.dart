import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../domain/route.dart';

/// Lista de rutas del chofer.
///
/// `AsyncNotifier` refresca desde la red y cae al cache si falla, asi que la
/// pantalla casi nunca ve un error: ve datos, posiblemente viejos, y la franja
/// superior le dice si esta sin conexion.
class RoutesController extends AsyncNotifier<List<DriverRoute>> {
  @override
  Future<List<DriverRoute>> build() =>
      ref.read(routesRepositoryProvider).routes();

  /// Conserva lo que ya se ve mientras actualiza: vaciar la lista para mostrar
  /// un spinner hace que la pantalla parpadee en cada deslizamiento.
  Future<void> refresh() async {
    state = const AsyncLoading<List<DriverRoute>>().copyWithPrevious(state);
    state = await AsyncValue.guard(
      () => ref.read(routesRepositoryProvider).routes(),
    );
  }
}

final routesControllerProvider =
    AsyncNotifierProvider<RoutesController, List<DriverRoute>>(
      RoutesController.new,
    );

/// Detalle de una ruta. Se indexa por identificador para que volver atras y
/// entrar de nuevo no recargue innecesariamente.
final routeDetailProvider =
    AsyncNotifierProvider.family<RouteDetailController, DriverRoute?, int>(
      RouteDetailController.new,
    );

class RouteDetailController extends FamilyAsyncNotifier<DriverRoute?, int> {
  @override
  Future<DriverRoute?> build(int rutaId) =>
      ref.read(routesRepositoryProvider).routeDetail(rutaId);

  Future<void> refresh() async {
    state = const AsyncLoading<DriverRoute?>().copyWithPrevious(state);
    state = await AsyncValue.guard(
      () => ref.read(routesRepositoryProvider).routeDetail(arg),
    );
  }

  /// Marca una entrega y recarga desde local.
  ///
  /// No se espera a la red: el repositorio ya aplico el cambio en la base del
  /// telefono y encolo el envio. Releer local es inmediato y muestra el estado
  /// nuevo al instante.
  Future<void> marcar({
    required int rutaPedidoId,
    required int pedidoId,
    required String estado,
    String? motivoFallo,
  }) async {
    await ref
        .read(routesRepositoryProvider)
        .marcarEntrega(
          rutaPedidoId: rutaPedidoId,
          pedidoId: pedidoId,
          estado: estado,
          motivoFallo: motivoFallo,
        );

    state = await AsyncValue.guard(
      () =>
          ref.read(routesRepositoryProvider).routeDetail(arg, forzarRed: false),
    );

    // La lista de rutas muestra contadores que acaban de cambiar.
    ref.invalidate(routesControllerProvider);
  }
}
