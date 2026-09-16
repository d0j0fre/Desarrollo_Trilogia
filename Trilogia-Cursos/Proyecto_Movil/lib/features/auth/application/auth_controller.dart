import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/providers.dart';
import '../../../core/storage/app_database.dart';
import '../../../core/storage/secure_store.dart';
import '../../../core/sync/sync_service.dart';
import '../data/auth_api.dart';
import '../domain/session.dart';

enum AuthStatus { desconocido, autenticado, anonimo }

class AuthState {
  const AuthState({
    this.status = AuthStatus.desconocido,
    this.session,
    this.error,
    this.cargando = false,
    this.capacidadesCargadas = false,
  });

  final AuthStatus status;
  final Session? session;
  final String? error;
  final bool cargando;

  /// Si ya se sabe que puede hacer esta persona, sea por el servidor o por lo
  /// guardado en el telefono.
  ///
  /// Sin este dato, una lista de modulos vacia es ambigua: puede significar
  /// "todavia no pregunte" o "el servidor dijo que ninguno". La pantalla de
  /// inicio necesita distinguirlas, porque decirle a alguien que no tiene
  /// permisos cuando en realidad falta señal es alarmante y falso.
  final bool capacidadesCargadas;

  AuthState copyWith({
    AuthStatus? status,
    Session? session,
    String? error,
    bool? cargando,
    bool? capacidadesCargadas,
    bool limpiarError = false,
  }) => AuthState(
    status: status ?? this.status,
    session: session ?? this.session,
    error: limpiarError ? null : (error ?? this.error),
    cargando: cargando ?? this.cargando,
    capacidadesCargadas: capacidadesCargadas ?? this.capacidadesCargadas,
  );
}

/// Sesion de la aplicacion.
///
/// Es la unica pieza que decide si hay usuario. El router la observa y
/// redirige solo; ninguna pantalla comprueba la sesion por su cuenta.
class AuthController extends StateNotifier<AuthState> {
  AuthController(this._ref) : super(const AuthState());

  final Ref _ref;

  SecureStore get _store => _ref.read(secureStoreProvider);
  AuthApi get _api => _ref.read(authApiProvider);
  SyncService get _sync => _ref.read(syncServiceProvider);
  AppDatabase get _database => _ref.read(appDatabaseProvider);

  /// Restaura la sesion al abrir la aplicacion.
  ///
  /// No valida el token contra el servidor: si esta vencido, la primera
  /// peticion lo renueva sola desde el interceptor. Validar aqui obligaria a
  /// tener señal para abrir la aplicacion, que es justo lo contrario de lo que
  /// se busca.
  Future<void> restore() async {
    final refreshToken = await _store.readRefreshToken();
    if (refreshToken == null || refreshToken.isEmpty) {
      state = state.copyWith(status: AuthStatus.anonimo);
      return;
    }

    final profile = await _store.readProfile();
    var session = Session(
      userId: int.tryParse(profile['userId'] ?? '0') ?? 0,
      fullName: profile['fullName'] ?? '',
      email: profile['email'] ?? '',
      role: profile['role'] ?? '',
    );

    // Lo ultimo que dijo el servidor, guardado en el telefono. Permite mostrar
    // la pantalla de inicio completa de inmediato, incluso sin señal.
    final guardadas = await _store.readCapabilities();
    final teniaGuardadas = guardadas != null;

    if (guardadas != null) {
      session = session.copyWith(
        permisos: (guardadas['permissions'] as List<dynamic>? ?? [])
            .map((item) => item.toString())
            .toList(),
        modulos: (guardadas['modules'] as List<dynamic>? ?? [])
            .map((item) => AppModule.fromJson(item as Map<String, dynamic>))
            .toList(),
      );
    }

    state = state.copyWith(
      status: AuthStatus.autenticado,
      session: session,
      capacidadesCargadas: teniaGuardadas,
    );
    _sync.start();

    // Y se refrescan contra el servidor en segundo plano, por si cambiaron.
    unawaited(_refreshCapabilities(session));
  }

  Future<bool> login({required String email, required String password}) async {
    state = state.copyWith(cargando: true, limpiarError: true);

    try {
      final info = await PackageInfo.fromPlatform();
      final deviceId = await _store.deviceId(
        () => _ref.read(uuidProvider).v4(),
      );

      final result = await _api.login(
        email: email,
        password: password,
        deviceId: deviceId,
        deviceName: info.appName,
      );

      await _store.saveSession(
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        accessExpiresAt: result.expiresAt,
        userId: result.session.userId,
        fullName: result.session.fullName,
        email: result.session.email,
        role: result.session.role,
      );

      state = AuthState(
        status: AuthStatus.autenticado,
        session: result.session,
      );

      _sync.start();
      unawaited(_refreshCapabilities(result.session));
      return true;
    } on ApiException catch (error) {
      state = state.copyWith(cargando: false, error: loginErrorMessage(error));
      return false;
    } on StateError catch (error) {
      state = state.copyWith(cargando: false, error: error.message);
      return false;
    } catch (_) {
      state = state.copyWith(
        cargando: false,
        error: 'No fue posible iniciar sesión. Intentá de nuevo.',
      );
      return false;
    }
  }

  /// Mensaje para una falla del login.
  ///
  /// Los mensajes genericos de [ApiException] estan pensados para acciones que
  /// pasan por la cola ("Se guardó y se enviará al recuperar señal"). El login
  /// no se encola: decir eso en esta pantalla es falso y deja a la persona
  /// esperando algo que nunca va a pasar.
  static String loginErrorMessage(ApiException error) =>
      switch (error.failure) {
        ApiFailure.sinConexion =>
          'No se pudo conectar con el servidor. Revisá tu conexión a internet '
              'e intentá de nuevo.',
        ApiFailure.tiempoAgotado =>
          'El servidor está tardando en responder. Esperá unos segundos e '
              'intentá de nuevo.',
        ApiFailure.servidor =>
          'El servidor no está disponible en este momento. Intentá de nuevo '
              'en unos minutos.',
        _ => error.message,
      };

  Future<void> _refreshCapabilities(Session session) async {
    try {
      final actualizada = await _api.capabilities(session);

      await _store.saveCapabilities({
        'permissions': actualizada.permisos,
        'modules': actualizada.modulos.map((m) => m.toJson()).toList(),
      });

      if (mounted) {
        state = state.copyWith(session: actualizada, capacidadesCargadas: true);
      }
    } catch (_) {
      // Sin señal se trabaja con lo ultimo conocido, si lo hay. La autorizacion
      // real la aplica el servidor en cada peticion de todas formas.
      //
      // Se atrapa todo y no solo ApiException a proposito: si esto falla por
      // cualquier motivo, la pantalla de inicio no puede quedar afirmando que
      // la persona no tiene permisos.
    }
  }

  /// Reintenta traer las capacidades. Lo usa el boton de la pantalla de inicio
  /// cuando la primera carga no alcanzo a completarse.
  Future<void> reintentarCapacidades() async {
    final actual = state.session;
    if (actual != null) await _refreshCapabilities(actual);
  }

  /// Cierre de sesion iniciado por el usuario.
  Future<void> logout() async {
    final refreshToken = await _store.readRefreshToken();

    if (refreshToken != null && refreshToken.isNotEmpty) {
      try {
        await _api.logout(refreshToken);
      } on ApiException {
        // Sin señal no se puede revocar ahora. El token se borra igual y
        // caduca solo; lo importante es que el telefono quede limpio.
      }
    }

    await _clearEverything();
  }

  /// La renovacion fallo: token revocado, vencido, o cadena cortada por
  /// reutilizacion. Se limpia sin intentar hablar con el servidor.
  Future<void> sessionLost() => _clearEverything();

  Future<void> _clearEverything() async {
    _sync.stop();
    await _store.clearSession();
    // Un telefono se pierde: no puede quedar la ruta de nadie despues de salir.
    await _database.clearUserData();
    await _sync.refreshCounters();

    state = const AuthState(status: AuthStatus.anonimo);
  }
}

final authControllerProvider = StateNotifierProvider<AuthController, AuthState>(
  (ref) => AuthController(ref),
);

/// Azucar para las pantallas que solo necesitan la sesion.
final sessionProvider = Provider<Session?>(
  (ref) => ref.watch(authControllerProvider).session,
);
