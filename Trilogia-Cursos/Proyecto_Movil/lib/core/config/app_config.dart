/// Configuracion de compilacion.
///
/// La direccion del servidor se puede cambiar con `--dart-define`, por ejemplo
/// para probar contra una API levantada en la red local:
///   flutter run --dart-define=API_BASE_URL=https://192.168.1.50:57540/
///
/// Si no se indica, apunta a la API publicada en Azure. Antes el valor por
/// defecto era `10.0.2.2`, la direccion del emulador: cualquier `flutter run`
/// sobre un telefono real sin el `--dart-define` quedaba apuntando a una
/// direccion inalcanzable, y el login respondia "Sin conexión" con señal
/// perfecta. La direccion no es un secreto (esta en la documentacion y en el
/// sitio web), asi que no hay nada que ganar escondiendola y si mucho que
/// perder.
class AppConfig {
  const AppConfig._();

  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'https://api-trilogia-free-cr01.azurewebsites.net/',
  );

  /// Nombre comercial. Un solo lugar para que el login, el inicio y el sistema
  /// operativo no se contradigan.
  static const String companyName = 'Distribuidora JJ';
  static const String appTagline = 'Operación en ruta';

  /// Tolerancia de arranque. El plan gratuito de Azure descarga la API tras un
  /// rato sin uso y la base se pausa: la primera peticion del dia puede tardar
  /// cerca de un minuto. Un timeout corto convierte eso en un error falso.
  static const Duration coldStartTimeout = Duration(seconds: 60);
  static const Duration requestTimeout = Duration(seconds: 25);

  /// Cada cuanto se revisa la cola. Solo toca la red si hay algo por enviar:
  /// con la cola vacia no consume nada del plan de Azure.
  static const Duration syncInterval = Duration(seconds: 30);

  static bool get isConfigured => apiBaseUrl.isNotEmpty;
}
