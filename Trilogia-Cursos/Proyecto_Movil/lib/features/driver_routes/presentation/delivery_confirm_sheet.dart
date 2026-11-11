import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../data/evidence_capture.dart';
import '../domain/evidence_draft.dart';
import 'signature_page.dart';

/// Confirmacion de «Entregado» con la evidencia que el chofer quiera adjuntar.
///
/// La foto y la firma son **opcionales**, igual que en la web (Rutas →
/// Evidencias): la entrega se puede confirmar sin ellas. Devuelve la lista de
/// evidencias ya guardadas en el telefono (puede ir vacia) o `null` si el chofer
/// cancela, y en ese caso borra lo que alcanzo a capturar.
Future<List<EvidenceDraft>?> showDeliveryConfirmSheet(
  BuildContext context, {
  required int pedidoId,
  required String cliente,
}) {
  return showModalBottomSheet<List<EvidenceDraft>>(
    context: context,
    isScrollControlled: true,
    builder: (_) => DeliveryConfirmSheet(pedidoId: pedidoId, cliente: cliente),
  );
}

class DeliveryConfirmSheet extends ConsumerStatefulWidget {
  const DeliveryConfirmSheet({
    super.key,
    required this.pedidoId,
    required this.cliente,
  });

  final int pedidoId;
  final String cliente;

  @override
  ConsumerState<DeliveryConfirmSheet> createState() =>
      _DeliveryConfirmSheetState();
}

class _DeliveryConfirmSheetState extends ConsumerState<DeliveryConfirmSheet> {
  /// Mismo tope que el servidor: 5 MB por archivo.
  static const _maxBytes = 5 * 1024 * 1024;
  static const _maxFotos = 3;

  final _fotos = <EvidenceDraft>[];
  EvidenceDraft? _firma;
  bool _ocupado = false;
  bool _confirmado = false;

  // `ref` no se puede leer en dispose: se guarda lo que hace falta para limpiar.
  late final _store = ref.read(evidenceFileStoreProvider);

  @override
  void dispose() {
    // Si se cerro sin confirmar (cancelar, deslizar hacia abajo, atras), lo
    // capturado no se queda en el telefono.
    if (!_confirmado) {
      for (final borrador in [..._fotos, ?_firma]) {
        _borrarSilencioso(borrador.path);
      }
    }
    super.dispose();
  }

  /// Borrar un archivo no debe romper la pantalla: si no se pudo, queda como
  /// huerfano y la limpieza del arranque lo recoge.
  Future<void> _borrarSilencioso(String? path) async {
    try {
      await _store.borrar(path);
    } on Object {
      // Ver arriba.
    }
  }

  void _aviso(String mensaje) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(mensaje)));
  }

  Future<void> _tomarFoto(EvidenceSource fuente) async {
    if (_ocupado) return;
    setState(() => _ocupado = true);

    try {
      final origen = await ref.read(evidenceCaptureProvider).foto(fuente);
      if (origen == null) return;

      final guid = ref.read(uuidProvider).v4();
      final guardada = await _store.importar(origen: origen, syncGuid: guid);

      if (guardada == null) {
        _aviso('Ese formato de imagen no se admite. Usá una foto JPG, PNG o WEBP.');
        return;
      }
      if (await File(guardada).length() > _maxBytes) {
        await _borrarSilencioso(guardada);
        _aviso('La foto pesa más de 5 MB. Tomá otra o elegí una más liviana.');
        return;
      }

      if (!mounted) {
        await _borrarSilencioso(guardada);
        return;
      }
      setState(
        () => _fotos.add(
          EvidenceDraft(tipo: EvidenceType.foto, syncGuid: guid, path: guardada),
        ),
      );
    } on Object {
      _aviso('No se pudo abrir la cámara. Revisá el permiso de la aplicación.');
    } finally {
      if (mounted) setState(() => _ocupado = false);
    }
  }

  Future<void> _firmar() async {
    if (_ocupado) return;

    final png = await Navigator.of(context).push<Uint8List>(
      MaterialPageRoute(
        fullscreenDialog: true,
        builder: (_) => SignaturePage(cliente: widget.cliente),
      ),
    );
    if (png == null || !mounted) return;

    final guid = ref.read(uuidProvider).v4();
    final ruta = await _store.guardarBytes(bytes: png, syncGuid: guid);
    await _borrarSilencioso(_firma?.path);

    if (!mounted) {
      await _borrarSilencioso(ruta);
      return;
    }
    setState(
      () => _firma = EvidenceDraft(
        tipo: EvidenceType.firma,
        syncGuid: guid,
        path: ruta,
      ),
    );
  }

  Future<void> _quitar(EvidenceDraft borrador) async {
    // Primero sale de la pantalla (suelta el archivo) y despues se borra.
    setState(() {
      _fotos.remove(borrador);
      if (_firma == borrador) _firma = null;
    });
    await WidgetsBinding.instance.endOfFrame;
    await _borrarSilencioso(borrador.path);
  }

  /// Cancelar descarta lo capturado ANTES de cerrar: las miniaturas salen de la
  /// pantalla (sueltan sus archivos) y despues se borran. Cerrar con un gesto
  /// (deslizar, atras) pasa por `dispose`, que hace lo mismo sin esperar.
  Future<void> _cancelar() async {
    final borradores = <EvidenceDraft>[..._fotos, ?_firma];
    setState(() {
      _fotos.clear();
      _firma = null;
    });
    await WidgetsBinding.instance.endOfFrame;
    for (final borrador in borradores) {
      await _borrarSilencioso(borrador.path);
    }
    if (mounted) Navigator.of(context).pop();
  }

  void _confirmar() {
    _confirmado = true;
    Navigator.of(context).pop(<EvidenceDraft>[..._fotos, ?_firma]);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final puedeOtraFoto = _fotos.length < _maxFotos;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          left: 24,
          right: 24,
          bottom: MediaQuery.viewInsetsOf(context).bottom + 16,
        ),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Align(
                alignment: Alignment.centerLeft,
                child: IconBadge(
                  icon: Icons.check_circle_outline_rounded,
                  color: BrandColors.action,
                  size: 52,
                ),
              ),
              const SizedBox(height: 16),
              Text('Confirmar entrega', style: theme.textTheme.headlineSmall),
              const SizedBox(height: 10),
              Text(
                'Vas a marcar el pedido #${widget.pedidoId} de ${widget.cliente} como entregado. '
                'Se le va a avisar al cliente.',
                style: theme.textTheme.bodyLarge?.copyWith(
                  color: BrandColors.textMuted,
                ),
              ),
              const SizedBox(height: 20),
              Text('Evidencia (opcional)', style: theme.textTheme.titleMedium),
              const SizedBox(height: 4),
              Text(
                'Una foto o la firma del cliente respaldan la entrega. '
                'Si no hay señal, se envían solas al recuperarla.',
                style: theme.textTheme.bodySmall,
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      key: const Key('tomar_foto'),
                      icon: const Icon(Icons.photo_camera_outlined, size: 20),
                      label: const Text('Tomar foto'),
                      onPressed: _ocupado || !puedeOtraFoto
                          ? null
                          : () => _tomarFoto(EvidenceSource.camara),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: OutlinedButton.icon(
                      key: const Key('firma_cliente'),
                      icon: const Icon(Icons.draw_outlined, size: 20),
                      label: Text(
                        _firma == null ? 'Firma del cliente' : 'Repetir firma',
                      ),
                      onPressed: _ocupado ? null : _firmar,
                    ),
                  ),
                ],
              ),
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton.icon(
                  key: const Key('foto_galeria'),
                  icon: const Icon(Icons.photo_library_outlined, size: 18),
                  label: const Text('Elegir de la galería'),
                  onPressed: _ocupado || !puedeOtraFoto
                      ? null
                      : () => _tomarFoto(EvidenceSource.galeria),
                ),
              ),
              if (_fotos.isNotEmpty || _firma != null) ...[
                const SizedBox(height: 4),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: [
                    for (final foto in _fotos)
                      _Miniatura(borrador: foto, onQuitar: () => _quitar(foto)),
                    if (_firma != null)
                      _Miniatura(
                        borrador: _firma!,
                        onQuitar: () => _quitar(_firma!),
                      ),
                  ],
                ),
              ],
              const SizedBox(height: 24),
              ElevatedButton(
                key: const Key('confirmar_entrega'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: BrandColors.action,
                ),
                onPressed: _ocupado ? null : _confirmar,
                child: const Text('Sí, fue entregado'),
              ),
              const SizedBox(height: 8),
              TextButton(
                style: TextButton.styleFrom(
                  foregroundColor: BrandColors.textMuted,
                  minimumSize: const Size.fromHeight(AppTheme.minTouchTarget),
                ),
                onPressed: _ocupado ? null : _cancelar,
                child: const Text('Cancelar'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Miniatura extends StatelessWidget {
  const _Miniatura({required this.borrador, required this.onQuitar});

  final EvidenceDraft borrador;
  final VoidCallback onQuitar;

  static const _lado = 84.0;
  static const _boton = 28.0;

  @override
  Widget build(BuildContext context) {
    // El boton de quitar queda DENTRO de los limites del Stack: lo que sobresale
    // de su padre no recibe toques.
    return SizedBox(
      width: _lado + _boton / 2,
      height: _lado + _boton / 2,
      child: Stack(
        children: [
          Positioned(
            left: 0,
            bottom: 0,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
              child: Container(
                width: _lado,
                height: _lado,
                color: BrandColors.white,
                child: Image.file(
                  File(borrador.path),
                  fit: borrador.esFirma ? BoxFit.contain : BoxFit.cover,
                  cacheWidth: 200,
                  errorBuilder: (_, _, _) =>
                      const Icon(Icons.broken_image_outlined),
                ),
              ),
            ),
          ),
          Positioned(
            left: 4,
            bottom: 4,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
              decoration: BoxDecoration(
                color: BrandColors.black.withValues(alpha: 0.65),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                borrador.esFirma ? 'Firma' : 'Foto',
                style: const TextStyle(color: BrandColors.white, fontSize: 11),
              ),
            ),
          ),
          Positioned(
            right: 0,
            top: 0,
            child: Semantics(
              label: 'Quitar ${borrador.esFirma ? "firma" : "foto"}',
              button: true,
              child: InkWell(
                onTap: onQuitar,
                customBorder: const CircleBorder(),
                child: Container(
                  width: _boton,
                  height: _boton,
                  decoration: const BoxDecoration(
                    color: BrandColors.danger,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.close_rounded,
                    size: 18,
                    color: BrandColors.white,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
