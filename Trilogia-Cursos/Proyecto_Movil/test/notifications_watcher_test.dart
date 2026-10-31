import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/features/notifications/application/notifications_watcher.dart';
import 'package:proyecto_movil/features/notifications/data/notifications_api.dart';

/// BUG-23-05: cada aviso nuevo (ruta asignada, mensaje directo) llega una sola
/// vez a la bandeja del teléfono.
void main() {
  AppNotification aviso(int id, {bool read = false, String entityType = 'Chat'}) => AppNotification(
        id: id,
        type: entityType == 'Chat' ? 'ChatMensaje' : 'RutaAsignada',
        title: 'Aviso $id',
        message: 'Mensaje $id',
        entityType: entityType,
        entityId: 5,
        read: read,
      );

  test('solo se anuncian los avisos sin leer posteriores al último mostrado', () {
    final items = [aviso(9), aviso(8, read: true), aviso(7), aviso(4)];

    final fresh = NotificationsWatcher.toAnnounce(items, 4);

    expect(fresh.map((item) => item.id), [7, 9]);
  });

  test('sin avisos nuevos no se anuncia nada', () {
    expect(NotificationsWatcher.toAnnounce([aviso(3), aviso(2)], 3), isEmpty);
  });

  test('un mensaje directo abre la bandeja; una ruta abre la ruta', () {
    expect(aviso(1).appLocation, isNull);
    expect(aviso(2, entityType: 'Ruta').appLocation, '/rutas/5');
  });
}
