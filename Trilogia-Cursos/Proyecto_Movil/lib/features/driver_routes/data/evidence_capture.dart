import 'package:image_picker/image_picker.dart';

/// De donde sale la foto.
enum EvidenceSource { camara, galeria }

/// Captura de fotos. Es una interfaz para que las pruebas no necesiten una
/// camara: en el telefono se usa [ImagePickerEvidenceCapture].
abstract class EvidenceCapture {
  /// Devuelve la ruta de la foto, o `null` si el chofer cancelo.
  Future<String?> foto(EvidenceSource fuente);
}

/// Camara o galeria del telefono, con el paquete `image_picker`.
///
/// La foto se reduce al capturarla (lado mayor 1600 px, JPEG calidad 80): una
/// foto de 12 MP por entrega, por cuarenta entregas, en datos moviles, no es
/// viable, y el servidor rechaza mas de 5 MB. Abrir la camara no pide el permiso
/// de camara en tiempo de ejecucion: lo gestiona el paquete.
class ImagePickerEvidenceCapture implements EvidenceCapture {
  ImagePickerEvidenceCapture({ImagePicker? picker})
    : _picker = picker ?? ImagePicker();

  final ImagePicker _picker;

  static const maxLado = 1600.0;
  static const calidad = 80;

  @override
  Future<String?> foto(EvidenceSource fuente) async {
    final archivo = await _picker.pickImage(
      source: fuente == EvidenceSource.camara
          ? ImageSource.camera
          : ImageSource.gallery,
      maxWidth: maxLado,
      maxHeight: maxLado,
      imageQuality: calidad,
    );
    return archivo?.path;
  }
}
