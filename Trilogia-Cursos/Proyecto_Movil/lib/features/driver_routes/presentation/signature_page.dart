import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:signature/signature.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';

/// Lienzo para que el cliente firme con el dedo.
///
/// Devuelve la firma como PNG (lo unico que acepta el servidor para una firma),
/// o `null` si el chofer cancela. Fondo blanco: un PNG transparente se veria
/// negro sobre negro en cualquier visor con fondo oscuro.
class SignaturePage extends StatefulWidget {
  const SignaturePage({super.key, this.cliente = ''});

  final String cliente;

  @override
  State<SignaturePage> createState() => _SignaturePageState();
}

class _SignaturePageState extends State<SignaturePage> {
  late final SignatureController _controller = SignatureController(
    penStrokeWidth: 3,
    penColor: BrandColors.black,
    exportBackgroundColor: BrandColors.white,
    onDrawEnd: () => setState(() {}),
  );

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _guardar() async {
    if (_controller.isEmpty) return;
    final Uint8List? png = await _controller.toPngBytes();
    if (!mounted) return;
    Navigator.of(context).pop(png);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Firma del cliente')),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                widget.cliente.isEmpty
                    ? 'Pedile al cliente que firme con el dedo dentro del recuadro.'
                    : 'Pedile a ${widget.cliente} que firme con el dedo dentro del recuadro.',
                style: theme.textTheme.bodyLarge?.copyWith(
                  color: BrandColors.textMuted,
                ),
              ),
              const SizedBox(height: 16),
              Expanded(
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    border: Border.all(color: BrandColors.border, width: 2),
                    borderRadius: BorderRadius.circular(AppTheme.radius),
                  ),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(AppTheme.radius - 2),
                    child: Signature(
                      key: const Key('lienzo_firma'),
                      controller: _controller,
                      backgroundColor: BrandColors.white,
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      icon: const Icon(Icons.cleaning_services_outlined, size: 20),
                      label: const Text('Borrar'),
                      onPressed: _controller.isEmpty
                          ? null
                          : () => setState(_controller.clear),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: ElevatedButton.icon(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: BrandColors.action,
                      ),
                      icon: const Icon(Icons.check_rounded, size: 20),
                      label: const Text('Usar firma'),
                      onPressed: _controller.isEmpty ? null : _guardar,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
