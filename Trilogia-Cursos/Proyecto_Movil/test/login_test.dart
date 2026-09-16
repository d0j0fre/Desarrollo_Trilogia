import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:proyecto_movil/core/config/app_config.dart';
import 'package:proyecto_movil/core/network/api_client.dart';
import 'package:proyecto_movil/core/network/api_exception.dart';
import 'package:proyecto_movil/core/storage/secure_store.dart';
import 'package:proyecto_movil/features/auth/application/auth_controller.dart';

class _MockStore extends Mock implements SecureStore {}

/// Responde siempre lo mismo, sin red.
class _FixedAdapter implements HttpClientAdapter {
  _FixedAdapter(this.status, this.body);

  final int status;
  final Map<String, dynamic> body;
  int llamadas = 0;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    llamadas++;
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

/// El login es la puerta de la aplicacion. Dos defectos lo dejaban diciendo
/// cosas falsas: "sin conexión, se guardó" cuando la direccion del servidor
/// estaba mal, y "la sesión expiró" cuando la contraseña era incorrecta.
void main() {
  group('Direccion del servidor', () {
    test('sin --dart-define apunta a la API publicada, no al emulador', () {
      expect(AppConfig.apiBaseUrl, startsWith('https://'));
      expect(AppConfig.apiBaseUrl, isNot(contains('10.0.2.2')));
      expect(AppConfig.apiBaseUrl, endsWith('/'));
    });
  });

  group('Credenciales incorrectas', () {
    late _MockStore store;
    late _FixedAdapter adapter;
    late int sesionesPerdidas;
    late ApiClient client;

    setUp(() {
      store = _MockStore();
      when(() => store.readAccessToken()).thenAnswer((_) async => null);
      when(() => store.readRefreshToken()).thenAnswer((_) async => 'viejo');

      adapter = _FixedAdapter(401, {
        'success': false,
        'message': 'Correo o contraseña incorrectos.',
      });
      sesionesPerdidas = 0;

      client = ApiClient(
        store: store,
        onSessionLost: () async => sesionesPerdidas++,
        dio: Dio()..httpClientAdapter = adapter,
      );
    });

    test('muestran el mensaje del servidor, no "la sesión expiró"', () async {
      await expectLater(
        client.post<Map<String, dynamic>>(
          'api/auth/login',
          anonymous: true,
          body: const {},
        ),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'Correo o contraseña incorrectos.',
          ),
        ),
      );
    });

    test('no intentan renovar ni borran la sesión guardada', () async {
      await expectLater(
        client.post<Map<String, dynamic>>(
          'api/auth/login',
          anonymous: true,
          body: const {},
        ),
        throwsA(isA<ApiException>()),
      );

      expect(sesionesPerdidas, 0);
      expect(adapter.llamadas, 1, reason: 'no debe llamar a auth/refresh');
      verifyNever(() => store.readRefreshToken());
    });
  });

  group('Mensajes del login', () {
    test('sin conexión no promete guardar nada', () {
      final mensaje = AuthController.loginErrorMessage(
        ApiException(
          ApiFailure.sinConexion,
          'Sin conexión. Se guardó y se enviará al recuperar señal.',
        ),
      );

      expect(mensaje, isNot(contains('Se guardó')));
      expect(mensaje, contains('conectar'));
    });

    test('tiempo agotado y servidor caído tienen su propio mensaje', () {
      expect(
        AuthController.loginErrorMessage(
          ApiException(ApiFailure.tiempoAgotado, 'Reintentando…'),
        ),
        isNot(contains('Reintentando')),
      );
      expect(
        AuthController.loginErrorMessage(
          ApiException(ApiFailure.servidor, 'Reintentando…'),
        ),
        isNot(contains('Reintentando')),
      );
    });

    test('los mensajes del servidor pasan tal cual', () {
      expect(
        AuthController.loginErrorMessage(
          ApiException(ApiFailure.demasiadasPeticiones, 'Esperá 5 minutos.'),
        ),
        'Esperá 5 minutos.',
      );
    });
  });
}
