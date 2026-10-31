import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Notificaciones del sistema (la bandeja de Android).
///
/// Desde Android 13 hace falta el permiso POST_NOTIFICATIONS, declarado en el
/// manifiesto y concedido por la persona. Sin el, Android descarta el aviso en
/// silencio: por eso se pide al entrar y se puede volver a pedir desde Mi cuenta.
class SystemNotifier {
  SystemNotifier([FlutterLocalNotificationsPlugin? plugin]) : _plugin = plugin ?? FlutterLocalNotificationsPlugin();

  final FlutterLocalNotificationsPlugin _plugin;
  bool _ready = false;

  /// Que hacer cuando tocan el aviso. Recibe la ruta de la aplicacion.
  void Function(String? location)? onTap;

  static const _channel = AndroidNotificationDetails(
    'avisos',
    'Avisos',
    channelDescription: 'Rutas asignadas, mensajes directos y otros avisos de tu cuenta',
    importance: Importance.high,
    priority: Priority.high,
  );

  AndroidFlutterLocalNotificationsPlugin? get _android =>
      _plugin.resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>();

  Future<void> init() async {
    if (_ready) return;
    await _plugin.initialize(
      settings: const InitializationSettings(android: AndroidInitializationSettings('@mipmap/ic_launcher')),
      onDidReceiveNotificationResponse: (response) => onTap?.call(response.payload),
    );
    _ready = true;

    // La aplicacion estaba cerrada y la abrieron tocando un aviso.
    final launch = await _plugin.getNotificationAppLaunchDetails();
    if (launch?.didNotificationLaunchApp ?? false) onTap?.call(launch!.notificationResponse?.payload);
  }

  Future<bool> requestPermission() async => await _android?.requestNotificationsPermission() ?? false;

  Future<bool> enabled() async => await _android?.areNotificationsEnabled() ?? false;

  Future<void> show({required int id, required String title, required String body, String? location}) async {
    await init();
    await _plugin.show(
      id: id,
      title: title,
      body: body,
      notificationDetails: const NotificationDetails(android: _channel),
      payload: location,
    );
  }
}

final systemNotifierProvider = Provider<SystemNotifier>((ref) => SystemNotifier());
