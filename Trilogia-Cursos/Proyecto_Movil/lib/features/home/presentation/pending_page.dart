import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/providers.dart';
import '../../../core/sync/outbox_entry.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';

final pendingEntriesProvider = FutureProvider.autoDispose<List<OutboxEntry>>((
  ref,
) async {
  // Se vuelve a leer cada vez que cambia el estado de la cola.
  ref.watch(syncStateProvider);
  final outbox = ref.watch(outboxRepositoryProvider);
  return [...await outbox.conflictos(), ...await outbox.pendientes()];
});

/// Lo que todavia no salio del telefono.
///
/// Esta pantalla es lo que vuelve confiable el modo sin conexion. Sin ella, el
/// chofer tiene que confiar en que la aplicacion "ya lo mando" y la oficina
/// tiene que creerle. Con ella, los dos ven la misma lista.
class PendingPage extends ConsumerWidget {
  const PendingPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final entries = ref.watch(pendingEntriesProvider);
    final sync = ref.watch(syncStateProvider).value;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Por enviar'),
        actions: [
          IconButton(
            icon: const Icon(Icons.sync_rounded),
            tooltip: 'Enviar ahora',
            onPressed: () => ref.read(syncServiceProvider).flush(),
          ),
        ],
      ),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: entries.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, _) => const ErrorView(
                message: 'No se pudo leer la cola del teléfono.',
              ),
              data: (lista) => lista.isEmpty
                  ? EmptyState(
                      icon: Icons.cloud_done_outlined,
                      color: BrandColors.delivered,
                      title: 'Todo al día',
                      message: sync?.ultimaSincronizacion == null
                          ? 'No hay nada esperando para enviarse.'
                          : 'Última sincronización: '
                                '${DateFormat('HH:mm').format(sync!.ultimaSincronizacion!)}',
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
                      itemCount: lista.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 10),
                      itemBuilder: (context, index) =>
                          _EntryCard(entry: lista[index]),
                    ),
            ),
          ),
        ],
      ),
    );
  }
}

class _EntryCard extends ConsumerWidget {
  const _EntryCard({required this.entry});

  final OutboxEntry entry;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final esConflicto = entry.estado == OutboxStatus.conflicto;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                IconBadge(
                  icon: _iconoPara(entry.tipo),
                  color: esConflicto ? BrandColors.failed : BrandColors.pending,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    entry.descripcion,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                const SizedBox(width: 8),
                StatusChip(
                  label: esConflicto ? 'Necesita atención' : 'En espera',
                  color: esConflicto ? BrandColors.failed : BrandColors.pending,
                  icon: esConflicto ? Icons.error_outline : Icons.schedule,
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              'Registrado ${DateFormat('dd/MM HH:mm').format(entry.creadoEn)}'
              '${entry.intentos > 0 ? " · ${entry.intentos} ${entry.intentos == 1 ? "intento" : "intentos"}" : ""}',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            if (entry.ultimoError != null) ...[
              const SizedBox(height: 12),
              InlineNotice(
                message: entry.ultimoError!,
                color: esConflicto ? BrandColors.failed : BrandColors.offline,
              ),
            ],
            if (esConflicto) ...[
              const SizedBox(height: 14),
              // La aplicacion no resuelve el conflicto sola. Si la oficina
              // cancelo el pedido mientras el chofer estaba sin señal, decidir
              // automaticamente es como se pierde inventario.
              Text(
                'Esta acción no se pudo aplicar. Confirmá con operaciones antes de decidir.',
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: 10),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => ref
                          .read(syncServiceProvider)
                          .reintentar(entry.syncGuid),
                      icon: const Icon(Icons.refresh_rounded, size: 20),
                      label: const Text('Reintentar'),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: BrandColors.danger,
                        side: BorderSide(
                          color: BrandColors.danger.withValues(alpha: 0.5),
                          width: 1.5,
                        ),
                      ),
                      onPressed: () => _descartar(context, ref),
                      icon: const Icon(Icons.delete_outline_rounded, size: 20),
                      label: const Text('Descartar'),
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  static IconData _iconoPara(String tipo) => switch (tipo) {
    'entrega' => Icons.inventory_2_outlined,
    'jornada_abrir' => Icons.play_circle_outline_rounded,
    'jornada_cerrar' => Icons.stop_circle_outlined,
    'inventario_movimiento' => Icons.swap_vert_rounded,
    'compra_recepcion' => Icons.move_to_inbox_rounded,
    'pedido_preparado' => Icons.inventory_rounded,
    'venta' => Icons.shopping_cart_outlined,
    _ => Icons.cloud_upload_outlined,
  };

  Future<void> _descartar(BuildContext context, WidgetRef ref) async {
    final confirmado = await confirmSheet(
      context,
      title: 'Descartar acción',
      message:
          '"${entry.descripcion}" no se va a enviar nunca. Esta acción no se puede deshacer.',
      confirmLabel: 'Descartar',
      confirmColor: BrandColors.danger,
      icon: Icons.delete_outline_rounded,
    );

    if (!confirmado) return;
    await ref.read(syncServiceProvider).descartar(entry.syncGuid);
  }
}
