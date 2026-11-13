import 'dart:io';
import 'dart:typed_data';

import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

/// Archivos de evidencia (fotos y firmas) esperando salir a la red.
///
/// Se guardan en el almacenamiento **propio** de la aplicacion, no en la
/// carpeta temporal de la camara: sin señal una foto puede esperar horas, y
/// Android vacia el cache cuando necesita espacio o la aplicacion se cierra. El
/// nombre del archivo es el `syncGuid` de su entrada en la cola, asi cada una
/// es facil de enlazar y de borrar.
class EvidenceFileStore {
  EvidenceFileStore({Future<Directory> Function()? baseDirectory})
    : _baseDirectory = baseDirectory ?? _defaultDirectory;

  final Future<Directory> Function() _baseDirectory;

  static Future<Directory> _defaultDirectory() async =>
      getApplicationSupportDirectory();

  /// Extensiones y tipos que el servidor acepta (JPG, PNG, WEBP). Cualquier otro
  /// formato, como HEIC, se rechaza aqui para no encolar algo que va a fallar.
  static ({String extension, String contentType})? tipoPara(String path) {
    return switch (p.extension(path).toLowerCase()) {
      '.jpg' || '.jpeg' => (extension: '.jpg', contentType: 'image/jpeg'),
      '.png' => (extension: '.png', contentType: 'image/png'),
      '.webp' => (extension: '.webp', contentType: 'image/webp'),
      _ => null,
    };
  }

  Future<Directory> _dir() async {
    final base = await _baseDirectory();
    final dir = Directory(p.join(base.path, 'evidencias'));
    if (!await dir.exists()) await dir.create(recursive: true);
    return dir;
  }

  /// Copia un archivo existente (la foto que acaba de tomar la camara) al
  /// almacenamiento propio. Devuelve `null` si el formato no es admitido.
  Future<String?> importar({
    required String origen,
    required String syncGuid,
  }) async {
    final tipo = tipoPara(origen);
    if (tipo == null) return null;

    final destino = p.join((await _dir()).path, '$syncGuid${tipo.extension}');
    await File(origen).copy(destino);
    return destino;
  }

  /// Guarda bytes ya generados (la firma, como PNG).
  Future<String> guardarBytes({
    required Uint8List bytes,
    required String syncGuid,
    String extension = '.png',
  }) async {
    final destino = p.join((await _dir()).path, '$syncGuid$extension');
    await File(destino).writeAsBytes(bytes, flush: true);
    return destino;
  }

  Future<bool> existe(String path) => File(path).exists();

  /// Borra un archivo. Si el sistema lo tiene abierto un instante (el visor de la
  /// miniatura, un antivirus) reintenta un par de veces antes de rendirse.
  Future<void> borrar(String? path) async {
    if (path == null || path.isEmpty) return;
    final file = File(path);

    for (var intento = 1; ; intento++) {
      try {
        if (await file.exists()) await file.delete();
        return;
      } on FileSystemException {
        if (intento >= 4) rethrow;
        await Future<void>.delayed(const Duration(milliseconds: 150));
      }
    }
  }

  /// Al cerrar sesion no puede quedar ninguna foto de nadie en el telefono.
  Future<void> borrarTodo() async {
    final dir = await _dir();
    await for (final entity in dir.list()) {
      if (entity is File) await entity.delete();
    }
  }

  /// Quita los archivos que ninguna entrada de la cola referencia (una captura
  /// que el chofer abandono, o un cierre inesperado). Solo toca los de mas de
  /// `minimo`, para no borrar uno que se esta capturando en este momento.
  Future<int> limpiarHuerfanos(
    Set<String> referenciados, {
    Duration minimo = const Duration(hours: 1),
  }) async {
    final limite = DateTime.now().subtract(minimo);
    var borrados = 0;

    await for (final entity in (await _dir()).list()) {
      if (entity is! File || referenciados.contains(entity.path)) continue;
      if ((await entity.lastModified()).isAfter(limite)) continue;
      await entity.delete();
      borrados++;
    }
    return borrados;
  }
}
