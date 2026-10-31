import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/features/notifications/data/notifications_api.dart';

/// BUG-08-01 / BUG-10-08: el chofer recibe un aviso cuando le asignan o le
/// reasignan una ruta, y el aviso lo lleva a esa ruta.
void main() {
  test('la bandeja trae el contador y los avisos de la API', () {
    final inbox = NotificationsInbox.fromJson({
      'noLeidas': 1,
      'items': [
        {
          'notificacionId': 7,
          'tipo': 'RutaReasignada',
          'titulo': 'Te reasignaron una ruta',
          'mensaje': 'Ruta RUT-1 · QA-10 Ruta (Planificada).',
          'entidadTipo': 'Ruta',
          'entidadId': 12,
          'leida': false,
          'fechaCreacion': '2026-10-30T18:30:00',
        },
      ],
    });

    expect(inbox.unread, 1);
    final item = inbox.items.single;
    expect(item.type, 'RutaReasignada');
    expect(item.read, isFalse);
    expect(item.appLocation, '/rutas/12');
  });

  test('un aviso sin ruta no navega a ningún lado', () {
    final item = AppNotification.fromJson({'notificacionId': 1, 'titulo': 'Aviso', 'mensaje': 'x'});
    expect(item.appLocation, isNull);
  });

  test('una respuesta sin avisos no rompe la bandeja', () {
    final inbox = NotificationsInbox.fromJson({'noLeidas': 0});
    expect(inbox.items, isEmpty);
  });
}
