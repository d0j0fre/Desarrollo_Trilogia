import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../data/warehouse_api.dart';

final pickingProvider = FutureProvider.autoDispose.family<List<PickingOrder>, bool>(
  (ref, prepared) => ref.watch(warehouseApiProvider).picking(prepared: prepared),
);

/// Pedidos marcados como preparados que todavía no salieron del teléfono. Se
/// leen de la cola local para sacarlos de la lista al instante, con o sin señal.
final queuedPreparedProvider = FutureProvider.autoDispose<Set<int>>((ref) async {
  ref.watch(syncStateProvider);
  final pending = await ref.watch(outboxRepositoryProvider).pendientes();
  return pending
      .where((entry) => entry.tipo == 'pedido_preparado')
      .map((entry) => int.tryParse(entry.endpoint.split('/').reversed.skip(1).first) ?? 0)
      .toSet();
});

class PickingPage extends ConsumerWidget {
  const PickingPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Cuando la cola termina de enviar, las dos listas quedaron viejas.
    ref.listen(syncStateProvider, (previous, next) {
      if ((previous?.value?.enviando ?? false) && !(next.value?.enviando ?? true)) {
        ref.invalidate(pickingProvider);
      }
    });

    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Pedidos por preparar'),
          bottom: const TabBar(
            labelColor: BrandColors.redPrimary,
            indicatorColor: BrandColors.redPrimary,
            unselectedLabelColor: BrandColors.textMuted,
            tabs: [Tab(text: 'Por preparar'), Tab(text: 'Preparados hoy')],
          ),
        ),
        body: const Column(
          children: [
            SyncBanner(),
            Expanded(child: TabBarView(children: [_PickingList(prepared: false), _PickingList(prepared: true)])),
          ],
        ),
      ),
    );
  }
}

class _PickingList extends ConsumerWidget {
  const _PickingList({required this.prepared});

  final bool prepared;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final queued = ref.watch(queuedPreparedProvider).value ?? const <int>{};
    final orders = ref.watch(pickingProvider(prepared)).whenData(
          (items) => prepared ? items : items.where((order) => !queued.contains(order.id)).toList(),
        );

    return AsyncListBody<PickingOrder>(
      value: orders,
      onRefresh: () async => ref.invalidate(pickingProvider(prepared)),
      emptyIcon: prepared ? Icons.inventory_2_outlined : Icons.task_alt_rounded,
      emptyTitle: prepared ? 'Nada preparado hoy' : 'Todo preparado',
      emptyMessage: prepared
          ? 'Los pedidos que marqués como preparados hoy aparecen acá.'
          : queued.isEmpty
              ? 'No hay pedidos esperando preparación.'
              : 'Los últimos que marcaste se están enviando.',
      itemBuilder: (context, order) => TapCard(
        onTap: prepared
            ? () => context.push('/pedidos/${order.id}')
            : () async {
                await context.push<bool>('/bodega/preparar/${order.id}');
                ref.invalidate(queuedPreparedProvider);
              },
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                IconBadge(
                  icon: prepared ? Icons.check_circle_outline_rounded : Icons.inventory_2_outlined,
                  color: prepared ? BrandColors.delivered : BrandColors.redPrimary,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(order.client, style: Theme.of(context).textTheme.titleMedium),
                      Text('Pedido #${order.id} · ${Fmt.shortDateTime(order.date)}', style: Theme.of(context).textTheme.bodySmall),
                    ],
                  ),
                ),
                if (!prepared) const Icon(Icons.chevron_right_rounded, color: BrandColors.textMuted),
              ],
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                StatusChip(label: '${order.lines} productos · ${order.units} unid.', color: BrandColors.inRoute, icon: Icons.widgets_outlined),
                if (order.deliveryType.isNotEmpty) StatusChip(label: order.deliveryType, color: BrandColors.textMuted, icon: Icons.local_shipping_outlined),
                if (order.routeCode.isNotEmpty) StatusChip(label: 'Ruta ${order.routeCode}', color: BrandColors.view, icon: Icons.route_outlined),
              ],
            ),
            if (prepared && order.preparedBy.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text('Preparado por ${order.preparedBy} · ${Fmt.shortDateTime(order.preparedAt)}', style: Theme.of(context).textTheme.bodySmall),
            ] else if (order.address.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text(order.address, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall),
            ],
          ],
        ),
      ),
    );
  }
}
