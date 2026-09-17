import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../config/app_config.dart';
import '../config/environment.dart';
import '../storage/secure_store.dart';
import 'api_exception.dart';

/// Cliente HTTP de la aplicacion.
///
/// Concentra tres comportamientos que no deberian repetirse en cada pantalla:
/// poner el Bearer, renovar la sesion cuando vence, y reintentar cuando la
/// falla es de red y no del usuario.
class ApiClient {
  ApiClient({
    required SecureStore store,
    required Future<void> Function() onSessionLost,
    Dio? dio,
  }) : _store = store,
       _onSessionLost = onSessionLost,
       _dio = dio ?? Dio() {
    _dio.options
      ..baseUrl = AppConfig.apiBaseUrl
      ..connectTimeout = AppConfig.coldStartTimeout
      ..receiveTimeout = AppConfig.requestTimeout
      ..sendTimeout = AppConfig.requestTimeout
      ..headers['Accept'] = 'application/json'
      ..validateStatus = (status) => status != null && status < 400;

    _dio.interceptors.add(
      InterceptorsWrapper(onRequest: _attachToken, onError: _handleError),
    );

    // Registro de red solo fuera de release. Un log de peticiones en un APK
    // publicado deja direcciones de clientes y tokens al alcance de cualquiera
    // con el telefono en la mano.
    if (!isProduction && kDebugMode) {
      _dio.interceptors.add(
        LogInterceptor(requestBody: false, responseBody: false),
      );
    }
  }

  final Dio _dio;
  final SecureStore _store;
  final Future<void> Function() _onSessionLost;

  /// Renovacion en curso. Si diez peticiones reciben 401 a la vez, una sola
  /// renueva y las demas esperan ese mismo resultado: si no, diez canjes
  /// simultaneos del mismo token de refresco harian que el servidor lo tome
  /// por reutilizacion y revoque la cadena entera.
  Future<bool>? _refreshInFlight;

  Dio get dio => _dio;

  Future<void> _attachToken(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (options.extra['anonimo'] != true) {
      final token = await _store.readAccessToken();
      if (token != null && token.isNotEmpty) {
        options.headers['Authorization'] = 'Bearer $token';
      }
    }
    handler.next(options);
  }

  Future<void> _handleError(
    DioException error,
    ErrorInterceptorHandler handler,
  ) async {
    final isUnauthorized = error.response?.statusCode == 401;
    final isRefreshCall = error.requestOptions.path.contains('auth/refresh');
    final alreadyRetried = error.requestOptions.extra['reintentado'] == true;
    // Una peticion anonima no lleva token, asi que un 401 ahi no es una sesion
    // vencida: en el login es "correo o contraseña incorrectos". Intentar
    // renovar en ese caso borraba la sesion guardada y tapaba el mensaje real.
    final isAnonymous = error.requestOptions.extra['anonimo'] == true;

    if (!isUnauthorized || isRefreshCall || alreadyRetried || isAnonymous) {
      handler.next(error);
      return;
    }

    final renewed = await _refreshSession();
    if (!renewed) {
      await _onSessionLost();
      handler.next(error);
      return;
    }

    try {
      final options = error.requestOptions;
      options.extra['reintentado'] = true;

      final token = await _store.readAccessToken();
      if (token != null) options.headers['Authorization'] = 'Bearer $token';

      handler.resolve(await _dio.fetch(options));
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }

  Future<bool> _refreshSession() {
    return _refreshInFlight ??= _performRefresh().whenComplete(() {
      _refreshInFlight = null;
    });
  }

  Future<bool> _performRefresh() async {
    final refreshToken = await _store.readRefreshToken();
    if (refreshToken == null || refreshToken.isEmpty) return false;

    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/auth/refresh',
        data: {'refreshToken': refreshToken},
        options: Options(extra: {'anonimo': true}),
      );

      final body = response.data;
      final access = body?['accessToken'] as String?;
      final refresh = body?['refreshToken'] as String?;
      final expiresAt = DateTime.tryParse(body?['expiresAt'] as String? ?? '');

      if (access == null || refresh == null || expiresAt == null) return false;

      await _store.saveTokens(
        accessToken: access,
        refreshToken: refresh,
        accessExpiresAt: expiresAt,
      );
      return true;
    } on DioException {
      // Token vencido, revocado, o la cadena cortada por reutilizacion. En los
      // tres casos hay que volver a entrar; la API responde igual a proposito.
      return false;
    }
  }

  /// GET con reintento. El `delay` crece para no castigar a un servidor que
  /// justo esta despertando.
  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? query,
    bool anonymous = false,
    int maxAttempts = 3,
  }) async {
    return _withRetry(
      maxAttempts,
      () => _dio.get<T>(
        path,
        queryParameters: query,
        options: Options(extra: {'anonimo': anonymous}),
      ),
    );
  }

  /// POST **sin** reintento automatico.
  ///
  /// Deliberado: las escrituras las reintenta la cola, que sabe que el
  /// `syncGuid` las hace seguras. Reintentar aqui tambien duplicaria el
  /// esfuerzo y volveria imposible razonar sobre cuantas veces se intento.
  ///
  /// `receiveTimeout` permite esperar mas en llamadas que suelen despertar al
  /// servidor, como el login de la mañana.
  Future<T> post<T>(
    String path, {
    Object? body,
    bool anonymous = false,
    Duration? receiveTimeout,
  }) async {
    try {
      final response = await _dio.post<T>(
        path,
        data: body,
        options: Options(
          extra: {'anonimo': anonymous},
          receiveTimeout: receiveTimeout,
        ),
      );
      return response.data as T;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<T> _withRetry<T>(
    int maxAttempts,
    Future<Response<T>> Function() send,
  ) async {
    var attempt = 0;
    var delay = const Duration(seconds: 1);

    while (true) {
      attempt++;
      try {
        final response = await send();
        return response.data as T;
      } on DioException catch (error) {
        final failure = ApiException.fromDio(error);
        if (!failure.esReintentable || attempt >= maxAttempts) throw failure;

        await Future<void>.delayed(delay);
        delay *= 2;
      }
    }
  }
}
