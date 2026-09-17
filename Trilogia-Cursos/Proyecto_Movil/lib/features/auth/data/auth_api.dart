import '../../../core/config/app_config.dart';
import '../../../core/network/api_client.dart';
import '../domain/session.dart';

/// Resultado del login. Se queda con los seis campos del contrato original y
/// los tokens que la API agrego de forma aditiva.
class LoginResult {
  const LoginResult({
    required this.session,
    required this.accessToken,
    required this.refreshToken,
    required this.expiresAt,
  });

  final Session session;
  final String accessToken;
  final String refreshToken;
  final DateTime expiresAt;
}

class AuthApi {
  AuthApi(this._api);

  final ApiClient _api;

  /// El `deviceId` no es opcional en la practica: su presencia es lo que hace
  /// que la API emita tokens. Sin el, el login responde como para el sitio web.
  Future<LoginResult> login({
    required String email,
    required String password,
    required String deviceId,
    required String deviceName,
  }) async {
    final body = await _api.post<Map<String, dynamic>>(
      'api/auth/login',
      anonymous: true,
      // Suele ser la primera peticion del dia y la que despierta a la base.
      receiveTimeout: AppConfig.coldStartTimeout,
      body: {
        'email': email.trim(),
        'password': password,
        'deviceId': deviceId,
        'deviceName': deviceName,
      },
    );

    final accessToken = body['accessToken'] as String?;
    final refreshToken = body['refreshToken'] as String?;
    final expiresAt = DateTime.tryParse(body['expiresAt'] as String? ?? '');

    if (accessToken == null || refreshToken == null || expiresAt == null) {
      // La API respondio como al sitio web. Pasa si el servidor es una version
      // anterior a la Fase 1: conviene decirlo claro en vez de fallar raro.
      throw StateError(
        'El servidor no entregó una sesión para la aplicación. '
        'Puede estar desactualizado.',
      );
    }

    return LoginResult(
      session: Session(
        userId: body['userId'] as int? ?? 0,
        fullName: body['fullName'] as String? ?? '',
        email: body['email'] as String? ?? '',
        role: body['role'] as String? ?? '',
        permisos: (body['permissions'] as List<dynamic>? ?? [])
            .map((item) => item.toString())
            .toList(),
      ),
      accessToken: accessToken,
      refreshToken: refreshToken,
      expiresAt: expiresAt,
    );
  }

  /// Cierra sesion del lado del servidor. Un token que solo se borra del
  /// telefono sigue siendo valido: eso no es cerrar sesion.
  Future<void> logout(String refreshToken) async {
    await _api.post<Map<String, dynamic>>(
      'api/auth/logout',
      anonymous: true,
      body: {'refreshToken': refreshToken},
    );
  }

  /// Permisos y modulos vigentes. Se piden en cada arranque para que revocar un
  /// permiso tenga efecto sin publicar un APK nuevo.
  Future<Session> capabilities(Session base) async {
    final body = await _api.get<Map<String, dynamic>>(
      'api/mobile/v1/me/capabilities',
    );

    return base.copyWith(
      permisos: (body['permissions'] as List<dynamic>? ?? [])
          .map((item) => item.toString())
          .toList(),
      modulos: (body['modules'] as List<dynamic>? ?? [])
          .map((item) => AppModule.fromJson(item as Map<String, dynamic>))
          .toList(),
    );
  }
}
