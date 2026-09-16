import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../../auth/application/auth_controller.dart';
import '../data/warehouse_api.dart';

final purchaseOrdersProvider = FutureProvider.autoDispose.family<List<PurchaseOrder>, String>(
  (ref, status) => ref.watch(warehouseApiProvider).purchaseOrders(status: status),
);

final purchaseOrderProvider = FutureProvider.autoDispose.family<PurchaseOrderDetail, int>(
  (ref, id) => ref.watch(warehouseApiProvider).purchaseOrder(id),
);

final purchaseSuggestionsProvider = FutureProvider.autoDispose<List<PurchaseSuggestion>>(
  (ref) => ref.watch(warehouseApiProvider).suggestions(),
);

/// Órdenes de compra.
///
/// Para bodega es "Recepción": solo lo que falta por recibir. Para el perfil
/// Compras es una consulta con filtros y, si tiene el permiso, las
/// sugerencias de reposición.
class PurchaseOrdersPage extends ConsumerStatefulWidget {
  const PurchaseOrdersPage({super.key, this.receivingMode = false});

  final bool receivingMode;

  @override
  ConsumerState<PurchaseOrdersPage> createState() => _PurchaseOrdersPageState();
}

class _PurchaseOrdersPageState extends ConsumerState<PurchaseOrdersPage> {
  static const _statuses = ['Abiertas', '', 'Recibida', 'CerradaConDiscrepancia', 'Cancelada'];
  String _status = 'Abiertas';

  @override
  Widget build(BuildContext context) {
    final canSuggest = ref.watch(sessionProvider)?.puede('COMPRAS_SUGERENCIAS_VER') ?? false;
    final showTabs = !widget.receivingMode && canSuggest;
    final list = _OrdersList(
      status: widget.receivingMode ? 'Abiertas' : _status,
      toolbar: widget.receivingMode
          ? null
          : FilterChipsBar<String>(
              options: _statuses,
              selected: _status,
              label: (s) => s.isEmpty ? 'Todas' : Fmt.status(s),
              onSelected: (s) => setState(() => _status = s),
            ),
      receivingMode: widget.receivingMode,
    );

    if (!showTabs) {
      return Scaffold(
        appBar: AppBar(title: Text(widget.receivingMode ? 'Recepción de compras' : 'Órdenes de compra')),
        body: Column(children: [const SyncBanner(), Expanded(child: list)]),
      );
    }

    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Compras'),
          bottom: const TabBar(
            labelColor: BrandColors.redPrimary,
            indicatorColor: BrandColors.redPrimary,
            unselectedLabelColor: BrandColors.textMuted,
            tabs: [Tab(text: 'Órdenes'), Tab(text: 'Sugerencias')],
          ),
        ),
        body: TabBarView(children: [list, const _SuggestionsList()]),
      ),
    );
  }
}

class _OrdersList extends ConsumerWidget {
  const _OrdersList({required this.status, required this.toolbar, required this.receivingMode});

  final String status;
  final Widget? toolbar;
  final bool receivingMode;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final orders = ref.watch(purchaseOrdersProvider(status));

    return Column(
      children: [
        if (toolbar != null) ListToolbar(filters: toolbar),
        Expanded(
          child: AsyncListBody<PurchaseOrder>(
            value: orders,
            onRefresh: () async => ref.invalidate(purchaseOrdersProvider(status)),
            emptyIcon: Icons.local_shipping_outlined,
            emptyTitle: receivingMode ? 'Nada por recibir' : 'Sin órdenes de compra',
            emptyMessage: receivingMode
                ? 'Cuando compras registre una orden pendiente va a aparecer acá.'
                : 'No hay órdenes con este filtro.',
            itemBuilder: (context, order) => TapCard(
              onTap: () => context.push('/compras/${order.id}'),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const IconBadge(icon: Icons.receipt_long_outlined, color: BrandColors.inRoute),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('Orden #${order.id}', style: Theme.of(context).textTheme.titleMedium),
                            Text(order.supplier, style: Theme.of(context).textTheme.bodySmall),
                          ],
                        ),
                      ),
                      StatusChip(label: Fmt.status(order.status), color: _statusColor(order.status)),
                    ],
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(
                        child: Text('${order.received} de ${order.ordered} unidades recibidas',
                            style: Theme.of(context).textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600)),
                      ),
                      Text(Fmt.money(order.total), style: Theme.of(context).textTheme.bodyMedium),
                    ],
                  ),
                  const SizedBox(height: 8),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(99),
                    child: LinearProgressIndicator(value: order.progress, minHeight: 8, color: BrandColors.delivered),
                  ),
                  const SizedBox(height: 6),
                  Text('Creada ${Fmt.date(order.createdAt)}', style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

Color _statusColor(String status) => switch (status) {
      'Recibida' => BrandColors.delivered,
      'RecibidaParcial' => BrandColors.inRoute,
      'Cancelada' => BrandColors.danger,
      'CerradaConDiscrepancia' => BrandColors.textMuted,
      _ => BrandColors.pending,
    };

class _SuggestionsList extends ConsumerWidget {
  const _SuggestionsList();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final suggestions = ref.watch(purchaseSuggestionsProvider);

    return AsyncListBody<PurchaseSuggestion>(
      value: suggestions,
      onRefresh: () async => ref.invalidate(purchaseSuggestionsProvider),
      emptyIcon: Icons.task_alt_rounded,
      emptyTitle: 'Nada que reponer',
      emptyMessage: 'Con las ventas de los últimos tres meses, el stock alcanza para dos meses más.',
      header: Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text(
          'Calculado con las ventas de los últimos 3 meses para cubrir 2 meses, más el stock mínimo.',
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ),
      itemBuilder: (context, item) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(item.name, style: Theme.of(context).textTheme.titleMedium),
                    const SizedBox(height: 4),
                    Text(
                      'Stock ${item.stock} · mínimo ${item.minStock} · vende ${item.monthlyAverage.toStringAsFixed(1)}/mes',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                    if (item.insufficientData) ...[
                      const SizedBox(height: 6),
                      const StatusChip(label: 'Sin ventas recientes', color: BrandColors.textMuted),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Column(
                children: [
                  Text('${item.suggested}', style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800, color: BrandColors.redPrimary)),
                  Text('sugerido', style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Detalle de una orden de compra, con recepción por línea.
class PurchaseOrderPage extends ConsumerWidget {
  const PurchaseOrderPage({super.key, required this.orderId});

  final int orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final order = ref.watch(purchaseOrderProvider(orderId));
    final canReceive = ref.watch(sessionProvider)?.puede('COMPRAS_ORDENES_RECIBIR') ?? false;

    // Cuando la cola termina de enviar una recepción, lo que se ve quedó viejo.
    ref.listen(syncStateProvider, (previous, next) {
      if ((previous?.value?.enviando ?? false) && !(next.value?.enviando ?? true)) {
        ref.invalidate(purchaseOrderProvider(orderId));
        ref.invalidate(purchaseOrdersProvider);
      }
    });

    return Scaffold(
      appBar: AppBar(title: Text('Orden #$orderId')),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: AsyncDetailBody<PurchaseOrderDetail>(
              value: order,
              onRefresh: () async => ref.invalidate(purchaseOrderProvider(orderId)),
              builder: (context, detail) => ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
                children: [
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Row(
                            children: [
                              Expanded(child: Text(detail.supplier, style: Theme.of(context).textTheme.titleLarge)),
                              StatusChip(label: Fmt.status(detail.status), color: _statusColor(detail.status)),
                            ],
                          ),
                          const SizedBox(height: 6),
                          InfoRow(icon: Icons.event_outlined, label: 'Creada', value: Fmt.date(detail.createdAt)),
                          if (detail.notes.isNotEmpty) InfoRow(icon: Icons.notes_rounded, label: 'Notas', value: detail.notes),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),
                  const SectionTitle('Productos'),
                  for (final line in detail.lines)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: _LineCard(
                        line: line,
                        onReceive: canReceive && detail.canReceive && line.pending > 0
                            ? () => _receive(context, ref, detail, line)
                            : null,
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _receive(BuildContext context, WidgetRef ref, PurchaseOrderDetail order, PurchaseOrderLine line) async {
    final quantity = await showModalBottomSheet<int>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _ReceiveSheet(line: line),
    );
    if (quantity == null || quantity <= 0) return;

    await ref.read(warehouseApiProvider).receiveLine(order: order, line: line, quantity: quantity);
    if (context.mounted) showSuccess(context, 'Recepción de $quantity registrada. Se envía en cuanto haya señal.');
  }
}

class _LineCard extends StatelessWidget {
  const _LineCard({required this.line, required this.onReceive});

  final PurchaseOrderLine line;
  final VoidCallback? onReceive;

  @override
  Widget build(BuildContext context) {
    final done = line.pending == 0;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(child: Text(line.product, style: Theme.of(context).textTheme.titleMedium)),
                if (done) const StatusChip(label: 'Completa', color: BrandColors.delivered, icon: Icons.check_rounded),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                _Qty(label: 'Pedido', value: line.ordered),
                _Qty(label: 'Recibido', value: line.received, color: BrandColors.delivered),
                _Qty(label: 'Pendiente', value: line.pending, color: done ? BrandColors.textMuted : BrandColors.pending),
              ],
            ),
            if (onReceive != null) ...[
              const SizedBox(height: 12),
              ElevatedButton.icon(
                style: ElevatedButton.styleFrom(backgroundColor: BrandColors.action),
                onPressed: onReceive,
                icon: const Icon(Icons.move_to_inbox_rounded),
                label: const Text('Recibir'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Qty extends StatelessWidget {
  const _Qty({required this.label, required this.value, this.color = BrandColors.black});

  final String label;
  final int value;
  final Color color;

  @override
  Widget build(BuildContext context) => Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('$value', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: color)),
            Text(label, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      );
}

class _ReceiveSheet extends StatefulWidget {
  const _ReceiveSheet({required this.line});

  final PurchaseOrderLine line;

  @override
  State<_ReceiveSheet> createState() => _ReceiveSheetState();
}

class _ReceiveSheetState extends State<_ReceiveSheet> {
  late int _quantity = widget.line.pending;

  @override
  Widget build(BuildContext context) {
    return SheetBody(
      title: 'Recibir mercadería',
      subtitle: '${widget.line.product}. Faltan ${widget.line.pending} unidades por recibir.',
      children: [
        Center(
          child: QuantityStepper(
            value: _quantity,
            min: 1,
            max: widget.line.pending,
            onChanged: (value) => setState(() => _quantity = value),
          ),
        ),
        const SizedBox(height: 8),
        Center(
          child: TextButton(
            onPressed: () => setState(() => _quantity = widget.line.pending),
            child: const Text('Recibir todo lo pendiente'),
          ),
        ),
        const SizedBox(height: 12),
        ElevatedButton.icon(
          style: ElevatedButton.styleFrom(backgroundColor: BrandColors.action),
          onPressed: () => Navigator.of(context).pop(_quantity),
          icon: const Icon(Icons.check_rounded),
          label: Text('Recibir $_quantity unidades'),
        ),
        const SizedBox(height: 4),
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
      ],
    );
  }
}
