import 'package:flutter/foundation.dart';

/// Tipos de evidencia que acepta la API. Los nombres son los del servidor.
class EvidenceType {
  const EvidenceType._();

  static const foto = 'Foto';
  static const firma = 'Firma';
}

/// Una foto o firma ya capturada, todavia sin entrar a la cola.
///
/// El archivo vive en el almacenamiento propio de la aplicacion (no en la
/// carpeta temporal de la camara, que Android puede vaciar), y `syncGuid` es el
/// identificador que viajara hasta el servidor: igual en cada reintento.
@immutable
class EvidenceDraft {
  const EvidenceDraft({
    required this.tipo,
    required this.syncGuid,
    required this.path,
  });

  final String tipo;
  final String syncGuid;
  final String path;

  bool get esFirma => tipo == EvidenceType.firma;
}
