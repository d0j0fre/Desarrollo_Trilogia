import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/providers.dart';
import '../../../shared/utils/formatters.dart';

/// Un aviso para quien usa la aplicacion: por ejemplo, la ruta que le asignaron
/// o le reasignaron al chofer.
class AppNotification {
  const AppNotification({
    required this.id,
    required this.type,
    required this.title,
    required this.message,
    required this.entityType,
    required this.entityId,
    required this.read,
    this.createdAt,
  });

  final int id;
  final String type;
  final String title;
  final String message;
  final String entityType;
  final int entityId;
  final bool read;
  final DateTime? createdAt;

  /// Ruta de la aplicacion a la que lleva el aviso, si lleva a alguna.
  String? get appLocation => entityType == 'Ruta' && entityId > 0 ? '/rutas/$entityId' : null;

  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
        id: J.integer(json['notificacionId']),
        type: J.text(json['tipo']),
        title: J.text(json['titulo']),
        message: J.text(json['mensaje']),
        entityType: J.text(json['entidadTipo']),
        entityId: J.integer(json['entidadId']),
        read: J.boolean(json['leida']),
        createdAt: J.date(json['fechaCreacion']),
      );
}

class NotificationsInbox {
  const NotificationsInbox({required this.unread, required this.items});

  final int unread;
  final List<AppNotification> items;

  factory NotificationsInbox.fromJson(Map<String, dynamic> json) => NotificationsInbox(
        unread: J.integer(json['noLeidas']),
        items: ((json['items'] as List<dynamic>?) ?? const [])
            .map((item) => AppNotification.fromJson(item as Map<String, dynamic>))
            .toList(),
      );
}

class NotificationsApi {
  NotificationsApi(this._api);

  final ApiClient _api;

  static const _base = 'api/mobile/v1/me/notifications';

  Future<NotificationsInbox> inbox() async =>
      NotificationsInbox.fromJson(await _api.get<Map<String, dynamic>>(_base));

  Future<void> markRead(int id) => _api.post<dynamic>('$_base/$id/read');

  Future<void> markAllRead() => _api.post<dynamic>('$_base/read-all');
}

final notificationsApiProvider = Provider<NotificationsApi>(
  (ref) => NotificationsApi(ref.watch(apiClientProvider)),
);

/// La bandeja. La campana del inicio y la pantalla de avisos leen de aqui.
final notificationsInboxProvider = FutureProvider.autoDispose<NotificationsInbox>(
  (ref) => ref.watch(notificationsApiProvider).inbox(),
);
