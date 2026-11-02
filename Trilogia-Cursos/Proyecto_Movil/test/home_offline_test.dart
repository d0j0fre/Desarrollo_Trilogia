import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:proyecto_movil/core/network/api_exception.dart';
import 'package:proyecto_movil/core/providers.dart';
import 'package:proyecto_movil/core/sync/sync_service.dart';
import 'package:proyecto_movil/core/theme/app_theme.dart';
import 'package:proyecto_movil/features/auth/application/auth_controller.dart';
import 'package:proyecto_movil/features/auth/domain/session.dart';
import 'package:proyecto_movil/features/home/presentation/home_page.dart';
import 'package:proyecto_movil/features/notifications/data/notifications_api.dart';
import 'package:proyecto_movil/features/notifications/presentation/notifications_page.dart';

/// G3-18: con la sesión abierta y sin señal, el inicio quedaba en gris porque la
/// campana leía `.value` de la bandeja de avisos con error.
void main() {
  final sinSenal = ApiException(ApiFailure.sinConexion, 'Sin conexión.');

  Widget app(Widget home) => ProviderScope(
        overrides: [
          authControllerProvider.overrideWith((ref) => _SesionAbierta(ref)),
          notificationsInboxProvider.overrideWith((ref) async => throw sinSenal),
          daySummaryProvider.overrideWith((ref) async => throw sinSenal),
          syncStateProvider.overrideWith((ref) => Stream.value(const SyncState(hayConexion: false, pendientes: 2))),
        ],
        child: MaterialApp.router(
          theme: AppTheme.build(),
          routerConfig: GoRouter(routes: [GoRoute(path: '/', builder: (_, _) => home)]),
        ),
      );

  testWidgets('sin señal, el inicio muestra sus tarjetas y la campana sin número', (tester) async {
    await tester.pumpWidget(app(const HomePage()));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('Mis rutas'), findsOneWidget);
    expect(find.text('Kilometraje'), findsOneWidget);
    expect(find.byTooltip('Notificaciones'), findsOneWidget);
    final campana = tester.widget<Badge>(
        find.descendant(of: find.byTooltip('Notificaciones'), matching: find.byType(Badge)));
    expect(campana.isLabelVisible, isFalse);
  });

  testWidgets('sin señal, Notificaciones muestra el error de la bandeja en vez de romperse', (tester) async {
    await tester.pumpWidget(app(const NotificationsPage()));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('Notificaciones'), findsOneWidget);
    expect(find.text('Marcar leídas'), findsNothing);
  });
}

class _SesionAbierta extends AuthController {
  _SesionAbierta(super.ref) {
    state = const AuthState(
      status: AuthStatus.autenticado,
      capacidadesCargadas: true,
      session: Session(
        userId: 1,
        fullName: 'Chofer Demo',
        email: 'chofer@x.test',
        role: 'Chofer',
        modulos: [
          AppModule(key: 'driver.routes', title: 'Mis rutas', icon: 'route', enabled: true),
          AppModule(key: 'fleet.mileage', title: 'Kilometraje', icon: 'speed', enabled: true),
        ],
      ),
    );
  }
}
