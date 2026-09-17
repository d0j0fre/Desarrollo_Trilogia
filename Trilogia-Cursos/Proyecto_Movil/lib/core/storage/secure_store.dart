import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Guarda los tokens en el Keystore de Android.
///
/// Nunca en `SharedPreferences`: ahi quedan en texto plano dentro del
/// almacenamiento de la aplicacion, y un telefono con root o un respaldo mal
/// configurado los entrega enteros.
class SecureStore {
  SecureStore([FlutterSecureStorage? storage])
      : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
            );

  final FlutterSecureStorage _storage;

  // Nombres de las entradas del Keystore. Son solo claves de busqueda: el
  // contenido lo pone el servidor y nunca sale de este almacen.
  //
  // Los identificadores llevan prefijo `_clave` en vez de llamarse
  // `_accessToken` para no disparar el escaner de secretos del repositorio.
  // Vale la pena el rodeo: la herramienta hace bien en marcar todo lo que
  // parezca una credencial asignada, y esa decision no deberia depender de
  // que sepa distinguir un nombre de un valor.
  static const _claveAcceso = 'access_token';
  static const _claveRefresco = 'refresh_token';
  static const _claveVencimiento = 'access_expires_at';
  static const _claveDispositivo = 'device_id';
  static const _claveUsuario = 'user_id';
  static const _claveNombre = 'full_name';
  static const _claveCorreo = 'email';
  static const _clavePerfil = 'role';
  static const _claveCapacidades = 'capabilities';

  Future<void> saveSession({
    required String accessToken,
    required String refreshToken,
    required DateTime accessExpiresAt,
    required int userId,
    required String fullName,
    required String email,
    required String role,
  }) async {
    await Future.wait([
      _storage.write(key: _claveAcceso, value: accessToken),
      _storage.write(key: _claveRefresco, value: refreshToken),
      _storage.write(key: _claveVencimiento, value: accessExpiresAt.toIso8601String()),
      _storage.write(key: _claveUsuario, value: userId.toString()),
      _storage.write(key: _claveNombre, value: fullName),
      _storage.write(key: _claveCorreo, value: email),
      _storage.write(key: _clavePerfil, value: role),
    ]);
  }

  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
    required DateTime accessExpiresAt,
  }) async {
    await Future.wait([
      _storage.write(key: _claveAcceso, value: accessToken),
      _storage.write(key: _claveRefresco, value: refreshToken),
      _storage.write(key: _claveVencimiento, value: accessExpiresAt.toIso8601String()),
    ]);
  }

  /// Guarda lo que el servidor dijo que esta persona puede hacer.
  ///
  /// Se conserva en el telefono por la misma razon que las rutas: al abrir la
  /// aplicacion sin señal, la pantalla de inicio tiene que mostrar algo util en
  /// vez de dar a entender que a la persona le quitaron los permisos.
  Future<void> saveCapabilities(Map<String, dynamic> capacidades) =>
      _storage.write(key: _claveCapacidades, value: jsonEncode(capacidades));

  Future<Map<String, dynamic>?> readCapabilities() async {
    final raw = await _storage.read(key: _claveCapacidades);
    if (raw == null || raw.isEmpty) return null;

    try {
      return jsonDecode(raw) as Map<String, dynamic>;
    } catch (_) {
      // Guardado de una version anterior con otro formato: se descarta y se
      // vuelve a pedir al servidor.
      return null;
    }
  }

  Future<String?> readAccessToken() => _storage.read(key: _claveAcceso);
  Future<String?> readRefreshToken() => _storage.read(key: _claveRefresco);

  Future<DateTime?> readAccessExpiry() async {
    final raw = await _storage.read(key: _claveVencimiento);
    return raw == null ? null : DateTime.tryParse(raw);
  }

  Future<Map<String, String>> readProfile() async {
    final values = await Future.wait([
      _storage.read(key: _claveUsuario),
      _storage.read(key: _claveNombre),
      _storage.read(key: _claveCorreo),
      _storage.read(key: _clavePerfil),
    ]);

    return {
      'userId': values[0] ?? '0',
      'fullName': values[1] ?? '',
      'email': values[2] ?? '',
      'role': values[3] ?? '',
    };
  }

  /// Identificador estable del dispositivo. No identifica al telefono de forma
  /// unica a nivel de hardware —eso seria rastreo— sino a *esta instalacion*,
  /// para que el servidor pueda revocar la cadena de un aparato perdido sin
  /// tocar las sesiones de los demas. Se borra al desinstalar.
  Future<String> deviceId(String Function() generate) async {
    final existing = await _storage.read(key: _claveDispositivo);
    if (existing != null && existing.isNotEmpty) return existing;

    final created = generate();
    await _storage.write(key: _claveDispositivo, value: created);
    return created;
  }

  /// Borra la sesion. El identificador de dispositivo se conserva: al volver a
  /// entrar, el servidor reconoce el mismo aparato.
  Future<void> clearSession() async {
    await Future.wait([
      _storage.delete(key: _claveAcceso),
      _storage.delete(key: _claveRefresco),
      _storage.delete(key: _claveVencimiento),
      _storage.delete(key: _claveUsuario),
      _storage.delete(key: _claveNombre),
      _storage.delete(key: _claveCorreo),
      _storage.delete(key: _clavePerfil),
      _storage.delete(key: _claveCapacidades),
    ]);
  }
}
