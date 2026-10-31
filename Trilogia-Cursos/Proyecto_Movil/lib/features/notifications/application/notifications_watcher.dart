import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/notifications/system_notifier.dart';
import '../../../core/providers.dart';
import '../data/notifications_api.dart';

/// Revisa la bandeja mientras la aplicacion esta viva (en pantalla o en
/// segundo plano) y convierte cada aviso nuevo en una notificacion del sistema:
/// ruta asignada, ruta reasignada, mensaje directo.
///
/// No hay servicio de push (Firebase) en el proyecto: con la aplicacion
/// cerrada por completo el aviso espera en la bandeja y se muestra al abrirla.
class NotificationsWatcher with WidgetsBindingObserver {
  NotificationsWatcher(this._ref);

  final Ref _ref;
  Timer? _timer;
  int? _userId;
  bool _checking = false;

  /// Cuantos avisos sueltos se muestran de una vez; el resto se resume.
  static const maxSeparate = 3;

  void start(int userId) {
    if (_userId == userId && _timer != null) return;
    stop();
    _userId = userId;
    WidgetsBinding.instance.addObserver(this);
    _timer = Timer.periodic(AppConfig.notificationsInterval, (_) => check());
    unawaited(_firstCheck());
  }

  /// El permiso se pide antes de la primera revision: un aviso mostrado
  /// mientras el dialogo sigue abierto se pierde.
  Future<void> _firstCheck() async {
    try {
      await _ref.read(systemNotifierProvider).requestPermission();
    } catch (_) {
      // Sin plugin (pruebas) o sin dialogo: se sigue igual.
    }
    await check();
  }

  void stop() {
    _timer?.cancel();
    _timer = null;
    _userId = null;
    WidgetsBinding.instance.removeObserver(this);
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) unawaited(check());
  }

  Future<void> check() async {
    final userId = _userId;
    if (_checking || userId == null) return;
    _checking = true;
    try {
      final inbox = await _ref.read(notificationsApiProvider).inbox();
      final store = _ref.read(secureStoreProvider);
      final lastId = await store.readLastNotifiedId(userId);
      final fresh = toAnnounce(inbox.items, lastId);
      if (fresh.isEmpty) return;

      // Con las notificaciones apagadas no se dan por mostrados: saldran
      // cuando la persona las active. La campana los cuenta igual.
      final notifier = _ref.read(systemNotifierProvider);
      if (!await notifier.enabled()) {
        _ref.invalidate(notificationsInboxProvider);
        return;
      }

      if (fresh.length <= maxSeparate) {
        for (final item in fresh) {
          await notifier.show(id: item.id, title: item.title, body: item.message, location: item.appLocation ?? '/notificaciones');
        }
      } else {
        await notifier.show(
          id: fresh.last.id,
          title: 'Tenés ${fresh.length} avisos nuevos',
          body: fresh.map((item) => item.title).take(4).join(' · '),
          location: '/notificaciones',
        );
      }

      await store.writeLastNotifiedId(userId, fresh.last.id);
      _ref.invalidate(notificationsInboxProvider);
    } on ApiException {
      // Sin señal o sin sesion: se vuelve a intentar en la siguiente vuelta.
    } catch (_) {
      // Un fallo del sistema de notificaciones no debe tumbar la aplicacion.
    } finally {
      _checking = false;
    }
  }

  /// Avisos sin leer que todavia no se mostraron, del mas viejo al mas nuevo.
  static List<AppNotification> toAnnounce(List<AppNotification> items, int lastNotifiedId) =>
      items.where((item) => !item.read && item.id > lastNotifiedId).toList()
        ..sort((a, b) => a.id.compareTo(b.id));
}

final notificationsWatcherProvider = Provider<NotificationsWatcher>((ref) {
  final watcher = NotificationsWatcher(ref);
  ref.onDispose(watcher.stop);
  return watcher;
});
