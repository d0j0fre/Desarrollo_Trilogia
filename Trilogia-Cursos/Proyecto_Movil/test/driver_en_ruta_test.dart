import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/core/providers.dart';
import 'package:proyecto_movil/core/theme/app_theme.dart';
import 'package:proyecto_movil/features/driver_routes/data/routes_repository.dart';
import 'package:proyecto_movil/features/driver_routes/domain/delivery.dart';
import 'package:proyecto_movil/features/driver_routes/domain/evidence_draft.dart';
import 'package:proyecto_movil/features/driver_routes/domain/route.dart';
import 'package:proyecto_movil/features/driver_routes/presentation/route_detail_page.dart';

/// G3-04: la web deja marcar una parada «En ruta» y la API ya acepta esa
/// transición; la app solo ofrecía Entregado y No entregado.
void main() {
  Delivery entrega(String estado) => Delivery(
        rutaPedidoId: 14,
        pedidoId: 54,
        secuencia: 1,
        estadoEntrega: estado,
        cliente: 'QA Cercano',
        telefono: '',
        direccion: 'Escazú',
        total: 5000,
      );

  Future<_RepoFalso> abrir(WidgetTester tester, String estado) async {
    final repo = _RepoFalso(
      DriverRoute(
        rutaId: 7,
        codigo: 'RUT-7',
        zona: 'GAM',
        estado: 'Despachada',
        vehiculoPlaca: 'CRJ-001',
        entregas: [entrega(estado)],
      ),
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: [routesRepositoryProvider.overrideWithValue(repo)],
        child: MaterialApp(
          theme: AppTheme.build(),
          home: Scaffold(
            body: SingleChildScrollView(
              child: DeliveryCard(delivery: entrega(estado), rutaId: 7),
            ),
          ),
        ),
      ),
    );
    return repo;
  }

  final botonEnRuta = find.widgetWithText(OutlinedButton, 'En ruta');

  testWidgets('una parada pendiente ofrece el botón En ruta', (tester) async {
    await abrir(tester, DeliveryStatus.pendiente);

    expect(botonEnRuta, findsOneWidget);
    expect(find.text('Entregado'), findsOneWidget);
    expect(find.text('No entregado'), findsOneWidget);
  });

  testWidgets('al confirmar, la app marca la entrega En ruta (estado EnRuta de la API)', (tester) async {
    final repo = await abrir(tester, DeliveryStatus.pendiente);

    await tester.ensureVisible(botonEnRuta);
    await tester.tap(botonEnRuta);
    await tester.pumpAndSettle();
    expect(find.text('Sí, voy en ruta'), findsOneWidget);
    expect(repo.marcadas, isEmpty, reason: 'Sin confirmar no se marca nada.');

    await tester.tap(find.text('Sí, voy en ruta'));
    await tester.pumpAndSettle();

    expect(repo.marcadas, [DeliveryStatus.enRuta]);
  });

  testWidgets('cancelar la confirmación no marca nada', (tester) async {
    final repo = await abrir(tester, DeliveryStatus.pendiente);

    await tester.ensureVisible(botonEnRuta);
    await tester.tap(botonEnRuta);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancelar'));
    await tester.pumpAndSettle();

    expect(repo.marcadas, isEmpty);
  });

  testWidgets('una parada que ya está En ruta no repite el botón', (tester) async {
    await abrir(tester, DeliveryStatus.enRuta);

    expect(botonEnRuta, findsNothing);
    // Sigue pudiendo cerrarse.
    expect(find.text('Entregado'), findsOneWidget);
  });

  testWidgets('una parada cerrada no ofrece ninguna acción', (tester) async {
    await abrir(tester, DeliveryStatus.entregado);

    expect(botonEnRuta, findsNothing);
    expect(find.widgetWithText(ElevatedButton, 'Entregado'), findsNothing);
  });
}

class _RepoFalso implements RoutesRepository {
  _RepoFalso(this._ruta);

  final DriverRoute _ruta;
  final marcadas = <String>[];

  @override
  Future<void> marcarEntrega({
    required int rutaPedidoId,
    required int pedidoId,
    required String estado,
    String? motivoFallo,
    List<EvidenceDraft> evidencias = const [],
  }) async {
    marcadas.add(estado);
  }

  @override
  Future<DriverRoute?> routeDetail(int rutaId, {bool forzarRed = true}) async =>
      _ruta;

  @override
  Future<List<DriverRoute>> routes({bool forzarRed = true}) async => [_ruta];

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
