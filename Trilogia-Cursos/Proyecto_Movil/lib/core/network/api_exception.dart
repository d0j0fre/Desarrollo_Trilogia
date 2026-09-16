import 'package:dio/dio.dart';

/// Falla de red o de servidor, ya traducida a algo sobre lo que la aplicacion
/// puede decidir.
///
/// El punto de este tipo es que ninguna pantalla tenga que mirar codigos HTTP.
/// La cola de sincronizacion tampoco: pregunta `esReintentable` y ya.
enum ApiFailure {
  /// Sin red, o la red se cayo a mitad. Se reintenta.
  sinConexion,

  /// El servidor tardo demasiado. En el plan gratuito de Azure suele ser la
  /// base despertando, asi que se reintenta.
  tiempoAgotado,

  /// 401. El token vencio y no se pudo renovar: hay que volver a entrar.
  sesionExpirada,

  /// 403. Autenticado, pero sin permiso. No se reintenta: no va a cambiar solo.
  sinPermiso,

  /// 404. No existe, o no es suyo. La API no los distingue a proposito.
  noEncontrado,

  /// 422. Regla de negocio: la ruta se cancelo, la jornada ya estaba cerrada.
  /// No se reintenta; el usuario tiene que decidir.
  reglaDeNegocio,

  /// 429. Demasiadas peticiones. Se reintenta mas lento.
  demasiadasPeticiones,

  /// 5xx. Se reintenta.
  servidor,

  /// Cualquier otra cosa.
  desconocido,
}

class ApiException implements Exception {
  ApiException(this.failure, this.message, {this.statusCode});

  final ApiFailure failure;
  final String message;
  final int? statusCode;

  /// Si vale la pena volver a intentar sola. Es lo que consulta la cola.
  bool get esReintentable => switch (failure) {
    ApiFailure.sinConexion ||
    ApiFailure.tiempoAgotado ||
    ApiFailure.demasiadasPeticiones ||
    ApiFailure.servidor => true,
    _ => false,
  };

  /// Un conflicto que el usuario tiene que ver y resolver. La aplicacion no
  /// decide sola: resolver conflictos automaticamente en logistica es como se
  /// pierde inventario.
  bool get esConflicto =>
      failure == ApiFailure.reglaDeNegocio ||
      failure == ApiFailure.noEncontrado;

  factory ApiException.fromDio(DioException error) {
    final status = error.response?.statusCode;

    if (error.type == DioExceptionType.connectionError ||
        error.type == DioExceptionType.connectionTimeout) {
      return ApiException(
        ApiFailure.sinConexion,
        'Sin conexión. Se guardó y se enviará al recuperar señal.',
      );
    }

    if (error.type == DioExceptionType.receiveTimeout ||
        error.type == DioExceptionType.sendTimeout) {
      return ApiException(
        ApiFailure.tiempoAgotado,
        'El servidor está tardando. Reintentando…',
      );
    }

    final serverMessage = _messageFrom(error.response?.data);

    return switch (status) {
      // Con mensaje del servidor es el login ("Correo o contraseña
      // incorrectos."); sin mensaje, el token de una peticion autenticada.
      401 => ApiException(
        ApiFailure.sesionExpirada,
        serverMessage ?? 'La sesión expiró. Iniciá sesión de nuevo.',
        statusCode: status,
      ),
      403 => ApiException(
        ApiFailure.sinPermiso,
        serverMessage ?? 'No tenés permiso para hacer esto.',
        statusCode: status,
      ),
      404 => ApiException(
        ApiFailure.noEncontrado,
        serverMessage ?? 'Esto ya no está disponible.',
        statusCode: status,
      ),
      422 => ApiException(
        ApiFailure.reglaDeNegocio,
        serverMessage ?? 'La operación no se puede aplicar.',
        statusCode: status,
      ),
      429 => ApiException(
        ApiFailure.demasiadasPeticiones,
        serverMessage ?? 'Demasiadas solicitudes seguidas. Esperá un momento.',
        statusCode: status,
      ),
      _ when status != null && status >= 500 => ApiException(
        ApiFailure.servidor,
        'El servidor no está disponible. Reintentando…',
        statusCode: status,
      ),
      _ => ApiException(
        ApiFailure.desconocido,
        serverMessage ?? 'No fue posible completar la operación.',
        statusCode: status,
      ),
    };
  }

  /// Lee el mensaje que manda la API. Nunca expone detalle interno: el servidor
  /// ya se encarga de no mandarlo.
  static String? _messageFrom(Object? data) {
    if (data is Map<String, dynamic>) {
      final message = data['message'];
      if (message is String && message.trim().isNotEmpty) return message;
    }
    return null;
  }

  @override
  String toString() => 'ApiException($failure, $message)';
}
