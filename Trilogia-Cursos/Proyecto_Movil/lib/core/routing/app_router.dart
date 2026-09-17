import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/application/auth_controller.dart';
import '../../features/auth/presentation/login_page.dart';
import '../../features/driver_routes/presentation/route_detail_page.dart';
import '../../features/driver_routes/presentation/routes_list_page.dart';
import '../../features/home/presentation/home_page.dart';
import '../../features/home/presentation/pending_page.dart';
import '../../features/home/presentation/update_required_page.dart';
import '../../features/mileage/presentation/mileage_page.dart';
import '../../features/management/presentation/dashboard_page.dart';
import '../../features/management/presentation/orders_pages.dart';
import '../../features/management/presentation/routes_management_pages.dart';
import '../../features/office/presentation/office_pages.dart';
import '../../features/office/presentation/staff_pages.dart';
import '../../features/sales/presentation/sales_pages.dart';
import '../../features/settings/presentation/settings_page.dart';
import '../../features/warehouse/data/warehouse_api.dart';
import '../../features/warehouse/presentation/movement_page.dart';
import '../../features/warehouse/presentation/picking_page.dart';
import '../../features/warehouse/presentation/purchasing_pages.dart';
import '../../features/warehouse/presentation/stock_page.dart';
import '../config/app_config.dart';
import '../theme/app_theme.dart';
import '../theme/brand_colors.dart';
import '../widgets/app_widgets.dart';

/// Navegacion con guardas.
///
/// La redireccion vive aqui y en ningun otro lado. Ninguna pantalla comprueba
/// la sesion por su cuenta: si lo hicieran, cada una podria equivocarse de
/// forma distinta y quedaria una que no comprueba nada.
///
/// El router se crea **una sola vez**. Antes se recreaba con cada cambio del
/// estado de sesion: un login fallido borraba el correo ya escrito, y la
/// llegada de los permisos en segundo plano sacaba al chofer de la ruta que
/// estaba viendo y lo devolvia al inicio. Ahora solo se vuelven a evaluar las
/// guardas, y solo cuando cambia algo que las afecta.
final routerProvider = Provider<GoRouter>((ref) {
  final guardas = ValueNotifier<int>(0);
  ref.onDispose(guardas.dispose);

  ref.listen(
    authControllerProvider.select((auth) => auth.status),
    (_, _) => guardas.value++,
  );
  ref.listen(
    versionCheckProvider.select((version) => version.value?.verdict),
    (_, _) => guardas.value++,
  );

  final router = GoRouter(
    initialLocation: '/',
    refreshListenable: guardas,
    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      final version = ref.read(versionCheckProvider);

      // La compuerta de version va primero: si esta compilacion ya no puede
      // operar, no tiene sentido mostrar nada mas, ni siquiera el login.
      final verdict = version.value?.verdict;
      if (verdict == VersionVerdict.bloqueada) {
        return state.matchedLocation == '/actualizar' ? null : '/actualizar';
      }
      if (state.matchedLocation == '/actualizar') return '/';

      final enSplash = state.matchedLocation == '/splash';

      return switch (auth.status) {
        AuthStatus.desconocido => enSplash ? null : '/splash',
        AuthStatus.anonimo =>
          state.matchedLocation == '/login' ? null : '/login',
        AuthStatus.autenticado =>
          (state.matchedLocation == '/login' || enSplash) ? '/' : null,
      };
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const _SplashPage()),
      GoRoute(path: '/login', builder: (_, _) => const LoginPage()),
      GoRoute(
        path: '/actualizar',
        builder: (_, _) => const UpdateRequiredPage(),
      ),
      GoRoute(path: '/', builder: (_, _) => const HomePage()),
      GoRoute(path: '/rutas', builder: (_, _) => const RoutesListPage()),
      GoRoute(
        path: '/rutas/:id',
        builder: (_, state) => RouteDetailPage(
          rutaId: int.tryParse(state.pathParameters['id'] ?? '') ?? 0,
        ),
      ),
      GoRoute(path: '/kilometraje', builder: (_, _) => const MileagePage()),
      GoRoute(path: '/pendientes', builder: (_, _) => const PendingPage()),
      GoRoute(path: '/ajustes', builder: (_, _) => const SettingsPage()),

      // Bodega
      GoRoute(path: '/bodega/preparar', builder: (_, _) => const PickingPage()),
      GoRoute(
        path: '/bodega/preparar/:id',
        builder: (_, state) => OrderDetailPage(orderId: _id(state), pickingMode: true),
      ),
      GoRoute(path: '/bodega/recepcion', builder: (_, _) => const PurchaseOrdersPage(receivingMode: true)),
      GoRoute(
        path: '/bodega/movimientos',
        builder: (_, state) => MovementPage(product: state.extra is Product ? state.extra as Product : null),
      ),
      GoRoute(path: '/bodega/inventario', builder: (_, _) => const StockPage()),
      GoRoute(path: '/compras', builder: (_, _) => const PurchaseOrdersPage()),
      GoRoute(path: '/compras/:id', builder: (_, state) => PurchaseOrderPage(orderId: _id(state))),

      // Gestión
      GoRoute(path: '/gestion/metricas', builder: (_, _) => const DashboardPage()),
      GoRoute(path: '/gestion/retenidos', builder: (_, _) => const ApprovalsPage()),
      GoRoute(path: '/gestion/rutas', builder: (_, _) => const ManagedRoutesPage()),
      GoRoute(path: '/gestion/rutas/:id', builder: (_, state) => ManagedRouteDetailPage(routeId: _id(state))),
      GoRoute(path: '/gestion/productos', builder: (_, _) => const StockPage(manageMode: true)),
      GoRoute(path: '/pedidos', builder: (_, _) => const OrdersPage()),
      GoRoute(path: '/pedidos/:id', builder: (_, state) => OrderDetailPage(orderId: _id(state))),

      // Ventas
      GoRoute(path: '/ventas', builder: (_, _) => const SalesPage()),
      GoRoute(path: '/ventas/nueva', builder: (_, _) => const NewSalePage()),

      // Oficina y personal
      GoRoute(path: '/oficina/liquidaciones', builder: (_, _) => const SettlementsPage()),
      GoRoute(path: '/oficina/facturas', builder: (_, _) => const InvoicesPage()),
      GoRoute(path: '/oficina/credito', builder: (_, _) => const CreditPage()),
      GoRoute(path: '/oficina/consultas', builder: (_, _) => const SupportPage()),
      GoRoute(path: '/oficina/bitacora', builder: (_, _) => const AuditPage()),
      GoRoute(path: '/personal/jornadas', builder: (_, _) => const AttendancePage()),
      GoRoute(path: '/personal/aprobaciones', builder: (_, _) => const AttendanceApprovalsPage()),
    ],
    errorBuilder: (context, state) => Scaffold(
      appBar: AppBar(title: const Text('Página no encontrada')),
      body: EmptyState(
        icon: Icons.explore_off_outlined,
        title: 'Esa pantalla no existe',
        message: 'Puede que el enlace esté viejo.',
        actionLabel: 'Volver al inicio',
        onAction: () => context.go('/'),
      ),
    ),
  );

  ref.onDispose(router.dispose);
  return router;
});

int _id(GoRouterState state) => int.tryParse(state.pathParameters['id'] ?? '') ?? 0;

class _SplashPage extends StatelessWidget {
  const _SplashPage();

  /// Continua sin corte la pantalla de arranque nativa (vino con el logo)
  /// mientras se restaura la sesion.
  @override
  Widget build(BuildContext context) {
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: AppTheme.overlayOnBrand,
      child: Scaffold(
        body: Container(
          width: double.infinity,
          decoration: const BoxDecoration(gradient: BrandColors.headerGradient),
          child: const Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              BrandLogo(size: 128),
              SizedBox(height: 24),
              Text(
                AppConfig.companyName,
                style: TextStyle(
                  color: BrandColors.white,
                  fontSize: 24,
                  fontWeight: FontWeight.w800,
                ),
              ),
              SizedBox(height: 28),
              SizedBox(
                width: 26,
                height: 26,
                child: CircularProgressIndicator(
                  strokeWidth: 2.6,
                  color: BrandColors.goldLight,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
