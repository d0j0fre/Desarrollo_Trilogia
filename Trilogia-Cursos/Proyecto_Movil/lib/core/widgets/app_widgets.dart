import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../providers.dart';
import '../sync/sync_service.dart';
import '../theme/app_theme.dart';
import '../theme/brand_colors.dart';

/// Logo de Distribuidora JJ.
///
/// La insignia es circular y trae su propio aro dorado; aqui solo se le agrega
/// una sombra suave para que se despegue del fondo vino.
class BrandLogo extends StatelessWidget {
  const BrandLogo({super.key, this.size = 96, this.shadow = true});

  static const asset = 'assets/branding/logo.png';

  final double size;
  final bool shadow;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Logo de Distribuidora JJ',
      image: true,
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          boxShadow: shadow
              ? [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.28),
                    blurRadius: size * 0.22,
                    offset: Offset(0, size * 0.06),
                  ),
                ]
              : null,
        ),
        child: Image.asset(
          asset,
          width: size,
          height: size,
          filterQuality: FilterQuality.high,
        ),
      ),
    );
  }
}

/// Icono dentro de un cuadro con esquinas redondeadas y fondo tenue del mismo
/// color. Da jerarquia sin recargar: se usa en modulos, tarjetas y hojas.
class IconBadge extends StatelessWidget {
  const IconBadge({
    super.key,
    required this.icon,
    this.color = BrandColors.redPrimary,
    this.size = 44,
  });

  final IconData icon;
  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(size * 0.3),
      ),
      child: Icon(icon, color: color, size: size * 0.52),
    );
  }
}

/// Titulo de seccion en versalitas discretas.
class SectionTitle extends StatelessWidget {
  const SectionTitle(this.text, {super.key, this.trailing});

  final String text;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(left: 4, bottom: 10),
      child: Row(
        children: [
          Expanded(
            child: Text(
              text.toUpperCase(),
              style: Theme.of(context).textTheme.titleSmall,
            ),
          ),
          ?trailing,
        ],
      ),
    );
  }
}

/// Franja de estado de la cola.
///
/// Persistente y arriba de todo, a proposito. La aplicacion **declara** su
/// estado en vez de dejar que el chofer lo adivine: la mitad de la confianza en
/// una herramienta offline es poder ver que lo que uno marco todavia no salio.
///
/// Tocarla lleva a "Por enviar", donde se ve el detalle.
class SyncBanner extends ConsumerWidget {
  const SyncBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sync = ref.watch(syncStateProvider).value ?? const SyncState();

    final visible = !(sync.hayConexion && sync.todoAlDia);
    final (color, icon, text) = _describe(sync);
    final enPendientes =
        GoRouterState.of(context).matchedLocation == '/pendientes';

    return AnimatedSize(
      duration: const Duration(milliseconds: 220),
      curve: Curves.easeOut,
      child: !visible
          ? const SizedBox(width: double.infinity)
          : Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
              child: Material(
                color: color,
                borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
                child: InkWell(
                  borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
                  onTap: enPendientes
                      ? null
                      : () => context.push('/pendientes'),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 14,
                      vertical: 12,
                    ),
                    child: Row(
                      children: [
                        Icon(icon, size: 20, color: BrandColors.white),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            text,
                            style: const TextStyle(
                              color: BrandColors.white,
                              fontSize: 14,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                        if (sync.enviando)
                          const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: BrandColors.white,
                            ),
                          )
                        else if (!enPendientes)
                          const Icon(
                            Icons.chevron_right_rounded,
                            color: BrandColors.white,
                          ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
    );
  }

  static (Color, IconData, String) _describe(SyncState sync) {
    if (sync.conflictos > 0) {
      return (
        BrandColors.danger,
        Icons.error_outline_rounded,
        '${sync.conflictos} ${sync.conflictos == 1 ? "acción necesita" : "acciones necesitan"} tu atención',
      );
    }

    if (!sync.hayConexion) {
      final pendientes = sync.pendientes == 0
          ? 'Sin conexión · trabajando con lo guardado'
          : 'Sin conexión · ${sync.pendientes} ${sync.pendientes == 1 ? "acción pendiente" : "acciones pendientes"}';
      return (BrandColors.offline, Icons.cloud_off_rounded, pendientes);
    }

    return (
      BrandColors.inRoute,
      Icons.cloud_upload_outlined,
      '${sync.pendientes} ${sync.pendientes == 1 ? "acción por enviar" : "acciones por enviar"}',
    );
  }
}

/// Pantalla vacia con una salida. Nunca un vacio mudo: siempre dice por que
/// esta vacio y que puede hacer la persona.
class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    required this.message,
    this.actionLabel,
    this.onAction,
    this.color = BrandColors.textMuted,
  });

  final IconData icon;
  final String title;
  final String message;
  final String? actionLabel;
  final VoidCallback? onAction;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 84,
              height: 84,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.1),
                shape: BoxShape.circle,
              ),
              child: Icon(icon, size: 40, color: color),
            ),
            const SizedBox(height: 18),
            Text(
              title,
              style: Theme.of(context).textTheme.titleLarge,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 8),
            Text(
              message,
              style: Theme.of(
                context,
              ).textTheme.bodyMedium?.copyWith(color: BrandColors.textMuted),
              textAlign: TextAlign.center,
            ),
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: 24),
              SizedBox(
                width: 220,
                child: OutlinedButton.icon(
                  onPressed: onAction,
                  icon: const Icon(Icons.refresh_rounded, size: 20),
                  label: Text(actionLabel!),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Error con reintento. El mensaje dice que paso y como seguir, nunca un
/// codigo ni una disculpa.
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, this.onRetry});

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return EmptyState(
      icon: Icons.wifi_off_rounded,
      title: 'No se pudo cargar',
      message: message,
      actionLabel: onRetry == null ? null : 'Reintentar',
      onAction: onRetry,
      color: BrandColors.offline,
    );
  }
}

/// Confirmacion para acciones que no se deshacen.
///
/// Una hoja, no un dialogo de un toque: marcar una entrega como no entregada
/// dispara un correo al cliente y cambia el inventario. Eso no puede pasar por
/// un roce accidental en el bolsillo.
Future<bool> confirmSheet(
  BuildContext context, {
  required String title,
  required String message,
  required String confirmLabel,
  Color confirmColor = BrandColors.action,
  IconData? icon,
}) async {
  final result = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (context) => SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (icon != null) ...[
              Align(
                alignment: Alignment.centerLeft,
                child: IconBadge(icon: icon, color: confirmColor, size: 52),
              ),
              const SizedBox(height: 16),
            ],
            Text(title, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 10),
            Text(
              message,
              style: Theme.of(
                context,
              ).textTheme.bodyLarge?.copyWith(color: BrandColors.textMuted),
            ),
            const SizedBox(height: 28),
            ElevatedButton(
              style: ElevatedButton.styleFrom(backgroundColor: confirmColor),
              onPressed: () => Navigator.of(context).pop(true),
              child: Text(confirmLabel),
            ),
            const SizedBox(height: 8),
            TextButton(
              style: TextButton.styleFrom(
                foregroundColor: BrandColors.textMuted,
                minimumSize: const Size.fromHeight(AppTheme.minTouchTarget),
              ),
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Cancelar'),
            ),
          ],
        ),
      ),
    ),
  );

  return result ?? false;
}

/// Etiqueta de estado. Forma, icono y color juntos: el color solo no basta
/// para quien no distingue rojo de verde.
class StatusChip extends StatelessWidget {
  const StatusChip({
    super.key,
    required this.label,
    required this.color,
    this.icon,
  });

  final String label;
  final Color color;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 14, color: color),
            const SizedBox(width: 5),
          ],
          Text(
            label,
            style: TextStyle(
              color: color,
              fontSize: 12.5,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.1,
            ),
          ),
        ],
      ),
    );
  }
}

/// Fila etiqueta–valor para fichas de datos.
class InfoRow extends StatelessWidget {
  const InfoRow({
    super.key,
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Icon(icon, size: 20, color: BrandColors.textMuted),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              label,
              style: Theme.of(
                context,
              ).textTheme.bodyMedium?.copyWith(color: BrandColors.textMuted),
            ),
          ),
          const SizedBox(width: 12),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: Theme.of(
                context,
              ).textTheme.bodyLarge?.copyWith(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}

/// Mensaje en linea (error o aviso) dentro de un formulario o tarjeta.
class InlineNotice extends StatelessWidget {
  const InlineNotice({
    super.key,
    required this.message,
    this.color = BrandColors.danger,
    this.icon = Icons.error_outline_rounded,
  });

  final String message;
  final Color color;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
        border: Border.all(color: color.withValues(alpha: 0.25)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: color, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: TextStyle(color: color, fontSize: 14, height: 1.35),
            ),
          ),
        ],
      ),
    );
  }
}
