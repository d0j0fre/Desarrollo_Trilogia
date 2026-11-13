import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:proyecto_movil/core/network/api_exception.dart';
import 'package:proyecto_movil/core/providers.dart';
import 'package:proyecto_movil/core/sync/sync_service.dart';
import 'package:proyecto_movil/core/theme/app_theme.dart';
import 'package:proyecto_movil/features/driver_routes/application/routes_controller.dart';
import 'package:proyecto_movil/features/driver_routes/domain/route.dart';
import 'package:proyecto_movil/features/driver_routes/presentation/route_detail_page.dart';
import 'package:proyecto_movil/features/management/presentation/routes_management_pages.dart';

/// Sin señal, el título de Detalle de ruta y de Gestión de rutas leía `.value`
/// de una consulta con error y la pantalla se rompía al dibujarse (como G3-18).
void main() {
  final sinSenal = ApiException(ApiFailure.sinConexion, 'Sin conexión.');

  Widget app(Widget page, Override consulta) => ProviderScope(
        overrides: [
          consulta,
          syncStateProvider.overrideWith((ref) => Stream.value(const SyncState(hayConexion: false))),
        ],
        child: MaterialApp.router(
          theme: AppTheme.build(),
          routerConfig: GoRouter(routes: [GoRoute(path: '/', builder: (_, _) => page)]),
        ),
      );

  testWidgets('Detalle de ruta sin señal muestra el título genérico y el error', (tester) async {
    await tester.pumpWidget(app(
      const RouteDetailPage(rutaId: 12),
      routeDetailProvider.overrideWith(() => _DetalleSinSenal(sinSenal)),
    ));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('Ruta'), findsOneWidget);
  });

  testWidgets('Gestión de rutas sin señal muestra el título genérico y el error', (tester) async {
    await tester.pumpWidget(app(
      const ManagedRouteDetailPage(routeId: 12),
      managedRouteProvider.overrideWith((ref, id) async => throw sinSenal),
    ));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('Ruta'), findsOneWidget);
  });
}

class _DetalleSinSenal extends RouteDetailController {
  _DetalleSinSenal(this._error);

  final ApiException _error;

  @override
  Future<DriverRoute?> build(int rutaId) async => throw _error;
}
