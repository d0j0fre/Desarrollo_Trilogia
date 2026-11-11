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

/// G3-04 (revision): la app NO ofrece boton «En ruta».
///
/// Despachar la ruta (sp_Rutas_Dispatch) ya pone todas sus paradas En ruta, y una
/// parada Pendiente solo existe en una ruta Planificada, donde el servidor rechaza
/// cualquier cambio de estado. El boton nunca tenia un caso en que sirviera; el
/// estado se muestra en la etiqueta de la tarjeta y las acciones son Entregado y
/// No entregado.
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

  for (final estado in [DeliveryStatus.pendiente, DeliveryStatus.enRuta]) {
    testWidgets('una parada $estado no ofrece «En ruta», solo cerrar la entrega', (tester) async {
      await abrir(tester, estado);

      expect(botonEnRuta, findsNothing);
      expect(find.text('Entregado'), findsOneWidget);
      expect(find.text('No entregado'), findsOneWidget);
    });
  }

  testWidgets('la etiqueta de la tarjeta muestra el estado En ruta', (tester) async {
    await abrir(tester, DeliveryStatus.enRuta);

    expect(find.text('En ruta'), findsOneWidget);
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
