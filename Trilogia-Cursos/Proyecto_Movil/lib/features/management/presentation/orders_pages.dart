import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../../../shared/utils/navigation_launcher.dart';
import '../../auth/application/auth_controller.dart';
import '../../warehouse/data/warehouse_api.dart';
import '../data/management_api.dart';

typedef _OrderQuery = ({String status, String search});

final ordersProvider = FutureProvider.autoDispose.family<List<OrderSummary>, _OrderQuery>(
  (ref, query) => ref.watch(managementApiProvider).orders(status: query.status, search: query.search),
);

final orderDetailProvider = FutureProvider.autoDispose.family<OrderDetail, int>(
  (ref, id) => ref.watch(managementApiProvider).order(id),
);

final retainedOrdersProvider = FutureProvider.autoDispose<List<RetainedOrder>>(
  (ref) => ref.watch(managementApiProvider).retained(),
);

// ── Listado ──────────────────────────────────────────────────────────────────

class OrdersPage extends ConsumerStatefulWidget {
  const OrdersPage({super.key});

  @override
  ConsumerState<OrdersPage> createState() => _OrdersPageState();
}

class _OrdersPageState extends ConsumerState<OrdersPage> {
  static const _statuses = ['', 'Pendiente', 'Aprobado', 'EnProceso', 'Retenido', 'Entregado', 'Cancelado'];

  String _status = '';
  String _search = '';

  _OrderQuery get _query => (status: _status, search: _search);

  @override
  Widget build(BuildContext context) {
    final orders = ref.watch(ordersProvider(_query));

    return Scaffold(
      appBar: AppBar(title: const Text('Pedidos')),
      body: Column(
        children: [
          ListToolbar(
            search: AppSearchField(
              hint: 'Cliente, vendedor o número de pedido',
              onChanged: (value) => setState(() => _search = value),
            ),
            filters: FilterChipsBar<String>(
              options: _statuses,
              selected: _status,
              label: (s) => s.isEmpty ? 'Todos' : Fmt.status(s),
              onSelected: (s) => setState(() => _status = s),
            ),
          ),
          Expanded(
            child: AsyncListBody<OrderSummary>(
              value: orders,
              onRefresh: () async => ref.invalidate(ordersProvider(_query)),
              emptyIcon: Icons.receipt_long_outlined,
              emptyTitle: 'Sin pedidos',
              emptyMessage: 'No hay pedidos con este filtro.',
              itemBuilder: (context, order) => _OrderCard(order: order),
            ),
          ),
        ],
      ),
    );
  }
}

class _OrderCard extends StatelessWidget {
  const _OrderCard({required this.order});

  final OrderSummary order;

  @override
  Widget build(BuildContext context) {
    final color = OrderStatusStyle.color(order.status);

    return TapCard(
      onTap: () => context.push('/pedidos/${order.id}'),
      child: Row(
        children: [
          IconBadge(icon: OrderStatusStyle.icon(order.status), color: color),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(order.client, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 2),
                Text(
                  '#${order.id} · ${Fmt.shortDateTime(order.date)}${order.seller.isEmpty ? '' : ' · ${order.seller}'}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 6,
                  runSpacing: 4,
                  children: [
                    StatusChip(label: Fmt.status(order.status), color: color),
                    if (order.invoiced) const StatusChip(label: 'Facturado', color: BrandColors.view, icon: Icons.receipt_rounded),
                    if (order.prepared) const StatusChip(label: 'Preparado', color: BrandColors.inRoute, icon: Icons.inventory_2_rounded),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Text(Fmt.money(order.total), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
        ],
      ),
    );
  }
}

// ── Detalle ──────────────────────────────────────────────────────────────────

/// Detalle de un pedido.
///
/// En modo preparación (bodega) las líneas se vuelven una lista de chequeo y
/// el botón para marcar el pedido listo solo se habilita con todo marcado:
/// es la forma más barata de que no salga un pedido incompleto.
class OrderDetailPage extends ConsumerStatefulWidget {
  const OrderDetailPage({super.key, required this.orderId, this.pickingMode = false});

  final int orderId;
  final bool pickingMode;

  @override
  ConsumerState<OrderDetailPage> createState() => _OrderDetailPageState();
}

class _OrderDetailPageState extends ConsumerState<OrderDetailPage> {
  final _checked = <int>{};
  bool _busy = false;

  Future<void> _changeStatus(OrderDetail order, String target) async {
    final cancelling = target == 'Cancelado';
    final confirmed = await confirmSheet(
      context,
      title: cancelling ? 'Cancelar pedido' : 'Cambiar a ${Fmt.status(target)}',
      message: cancelling
          ? 'El pedido #${order.id} de ${order.client} se cancela. Si ya se había descontado inventario, se devuelve.'
          : 'El pedido #${order.id} pasa de ${Fmt.status(order.status)} a ${Fmt.status(target)}.',
      confirmLabel: cancelling ? 'Cancelar pedido' : 'Confirmar',
      confirmColor: cancelling ? BrandColors.danger : BrandColors.action,
      icon: cancelling ? Icons.cancel_outlined : OrderStatusStyle.icon(target),
    );
    if (!confirmed || !mounted) return;

    setState(() => _busy = true);
    try {
      await ref.read(managementApiProvider).changeStatus(order.id, target);
      ref.invalidate(orderDetailProvider(order.id));
      ref.invalidate(ordersProvider);
      if (mounted) showSuccess(context, 'Pedido #${order.id}: ${Fmt.status(target)}.');
    } catch (error) {
      if (mounted) showError(context, error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _markPrepared(OrderDetail order) async {
    await ref.read(warehouseApiProvider).markPrepared(orderId: order.id, client: order.client);
    if (!mounted) return;
    showSuccess(context, 'Pedido #${order.id} marcado como preparado.');
    Navigator.of(context).pop(true);
  }

  @override
  Widget build(BuildContext context) {
    final order = ref.watch(orderDetailProvider(widget.orderId));
    final canChange = ref.watch(sessionProvider)?.puede('PEDIDOS_CAMBIAR_ESTADO') ?? false;

    return Scaffold(
      appBar: AppBar(title: Text('Pedido #${widget.orderId}')),
      body: AsyncDetailBody<OrderDetail>(
        value: order,
        onRefresh: () async => ref.invalidate(orderDetailProvider(widget.orderId)),
        builder: (context, detail) {
          final allChecked = detail.lines.isNotEmpty && _checked.length == detail.lines.length;
          final alreadyPrepared = detail.preparedBy.isNotEmpty;

          return ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
            children: [
              _Header(order: detail),
              const SizedBox(height: 16),
              SectionTitle(widget.pickingMode ? 'Productos a alistar' : 'Productos',
                  trailing: widget.pickingMode && !alreadyPrepared
                      ? Text('${_checked.length}/${detail.lines.length}', style: Theme.of(context).textTheme.titleSmall)
                      : null),
              Card(
                child: Column(
                  children: [
                    for (var i = 0; i < detail.lines.length; i++) ...[
                      if (i > 0) const Divider(indent: 16, endIndent: 16),
                      widget.pickingMode && !alreadyPrepared
                          ? CheckboxListTile(
                              value: _checked.contains(i),
                              onChanged: (value) => setState(() => value == true ? _checked.add(i) : _checked.remove(i)),
                              controlAffinity: ListTileControlAffinity.leading,
                              activeColor: BrandColors.action,
                              title: Text(detail.lines[i].name, style: const TextStyle(fontWeight: FontWeight.w600)),
                              subtitle: Text('Stock actual: ${detail.lines[i].stock}'),
                              secondary: _QtyBadge(quantity: detail.lines[i].quantity),
                            )
                          : ListTile(
                              title: Text(detail.lines[i].name, style: const TextStyle(fontWeight: FontWeight.w600)),
                              subtitle: Text('${detail.lines[i].quantity} × ${Fmt.money(detail.lines[i].unitPrice)}'),
                              trailing: Text(Fmt.money(detail.lines[i].subtotal), style: const TextStyle(fontWeight: FontWeight.w700)),
                            ),
                    ],
                    const Divider(),
                    ListTile(
                      title: const Text('Total', style: TextStyle(fontWeight: FontWeight.w800)),
                      trailing: Text(Fmt.money(detail.total), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              if (widget.pickingMode) ...[
                if (alreadyPrepared)
                  InlineNotice(
                    color: BrandColors.delivered,
                    icon: Icons.check_circle_outline_rounded,
                    message: 'Preparado por ${detail.preparedBy} el ${Fmt.dateTime(detail.preparedAt)}.',
                  )
                else ...[
                  if (!allChecked)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Text('Marcá cada producto a medida que lo alistás.', textAlign: TextAlign.center,
                          style: Theme.of(context).textTheme.bodySmall),
                    ),
                  ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(backgroundColor: BrandColors.action),
                    onPressed: allChecked ? () => _markPrepared(detail) : null,
                    icon: const Icon(Icons.inventory_rounded),
                    label: const Text('Marcar como preparado'),
                  ),
                ],
              ] else if (canChange && detail.allowedTransitions.isNotEmpty) ...[
                const SectionTitle('Cambiar estado'),
                for (final target in detail.allowedTransitions)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: target == 'Cancelado'
                        ? OutlinedButton.icon(
                            style: OutlinedButton.styleFrom(
                              foregroundColor: BrandColors.danger,
                              minimumSize: const Size.fromHeight(AppTheme.buttonHeight),
                            ),
                            onPressed: _busy ? null : () => _changeStatus(detail, target),
                            icon: const Icon(Icons.cancel_outlined),
                            label: const Text('Cancelar pedido'),
                          )
                        : BusyButton(
                            label: 'Marcar como ${Fmt.status(target).toLowerCase()}',
                            icon: OrderStatusStyle.icon(target),
                            busy: _busy,
                            color: BrandColors.action,
                            onPressed: () => _changeStatus(detail, target),
                          ),
                  ),
              ] else if (detail.status == 'Retenido')
                const InlineNotice(
                  color: BrandColors.pending,
                  icon: Icons.pause_circle_outline_rounded,
                  message: 'Este pedido espera aprobación de gerencia en "Pedidos retenidos".',
                ),
            ],
          );
        },
      ),
    );
  }
}

class _QtyBadge extends StatelessWidget {
  const _QtyBadge({required this.quantity});

  final int quantity;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(color: BrandColors.redPrimary, borderRadius: BorderRadius.circular(10)),
        child: Text('×$quantity', style: const TextStyle(color: BrandColors.white, fontWeight: FontWeight.w800, fontSize: 16)),
      );
}

class _Header extends StatelessWidget {
  const _Header({required this.order});

  final OrderDetail order;

  @override
  Widget build(BuildContext context) {
    final color = OrderStatusStyle.color(order.status);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(child: Text(order.client, style: Theme.of(context).textTheme.titleLarge)),
                StatusChip(label: Fmt.status(order.status), color: color, icon: OrderStatusStyle.icon(order.status)),
              ],
            ),
            const SizedBox(height: 8),
            InfoRow(icon: Icons.event_outlined, label: 'Fecha', value: Fmt.dateTime(order.date)),
            InfoRow(icon: Icons.local_shipping_outlined, label: 'Entrega', value: order.deliveryType.isEmpty ? '—' : order.deliveryType),
            if (order.seller.isNotEmpty) InfoRow(icon: Icons.badge_outlined, label: 'Vendedor', value: order.seller),
            if (order.invoiceNumber.isNotEmpty) InfoRow(icon: Icons.receipt_outlined, label: 'Factura', value: order.invoiceNumber),
            if (order.routeCode.isNotEmpty)
              InfoRow(icon: Icons.route_outlined, label: 'Ruta', value: '${order.routeCode}${order.deliveryStatus.isEmpty ? '' : ' · ${Fmt.status(order.deliveryStatus)}'}'),
            if (order.preparedBy.isNotEmpty) InfoRow(icon: Icons.inventory_2_outlined, label: 'Preparado por', value: order.preparedBy),
            if (order.address.isNotEmpty) ...[
              const SizedBox(height: 8),
              InlineNotice(message: order.address, color: BrandColors.inRoute, icon: Icons.location_on_outlined),
            ],
            if (order.notes.isNotEmpty) ...[
              const SizedBox(height: 8),
              InlineNotice(message: order.notes, color: BrandColors.textMuted, icon: Icons.notes_rounded),
            ],
            if (order.rejectionReason.isNotEmpty) ...[
              const SizedBox(height: 8),
              InlineNotice(message: 'Rechazado: ${order.rejectionReason}'),
            ],
            if (order.clientPhone.isNotEmpty) ...[
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: () => NavigationLauncher.llamar(order.clientPhone),
                icon: const Icon(Icons.phone_outlined, size: 20),
                label: Text('Llamar a ${order.clientPhone}'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

// ── Retenidos ────────────────────────────────────────────────────────────────

/// Pedidos que superaron el umbral de la venta móvil. Aprobar descuenta
/// inventario y factura en la misma operación del sitio web.
class ApprovalsPage extends ConsumerWidget {
  const ApprovalsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final retained = ref.watch(retainedOrdersProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Pedidos retenidos')),
      body: AsyncListBody<RetainedOrder>(
        value: retained,
        onRefresh: () async => ref.invalidate(retainedOrdersProvider),
        emptyIcon: Icons.verified_outlined,
        emptyTitle: 'Nada por aprobar',
        emptyMessage: 'Cuando una venta supere el umbral de retención va a aparecer acá.',
        itemBuilder: (context, order) => _RetainedCard(order: order),
      ),
    );
  }
}

class _RetainedCard extends ConsumerStatefulWidget {
  const _RetainedCard({required this.order});

  final RetainedOrder order;

  @override
  ConsumerState<_RetainedCard> createState() => _RetainedCardState();
}

class _RetainedCardState extends ConsumerState<_RetainedCard> {
  bool _busy = false;

  Future<void> _approve() async {
    final order = widget.order;
    final confirmed = await confirmSheet(
      context,
      title: 'Aprobar pedido',
      message: 'Pedido #${order.id} de ${order.client} por ${Fmt.money(order.total)}. '
          'Se descuenta el inventario y se genera la factura.',
      confirmLabel: 'Aprobar y facturar',
      icon: Icons.verified_rounded,
    );
    if (!confirmed || !mounted) return;

    setState(() => _busy = true);
    try {
      final invoice = await ref.read(managementApiProvider).approve(order.id);
      ref.invalidate(retainedOrdersProvider);
      if (mounted) showSuccess(context, 'Pedido aprobado. Factura $invoice.');
    } catch (error) {
      if (mounted) showError(context, error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _reject() async {
    final reason = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (_) => const ReasonSheet(
        title: 'Rechazar pedido',
        subtitle: 'El vendedor va a ver este motivo.',
        hint: 'Motivo del rechazo',
        confirmLabel: 'Rechazar pedido',
        danger: true,
      ),
    );
    if (reason == null || !mounted) return;

    setState(() => _busy = true);
    try {
      await ref.read(managementApiProvider).reject(widget.order.id, reason);
      ref.invalidate(retainedOrdersProvider);
      if (mounted) showSuccess(context, 'Pedido #${widget.order.id} rechazado.');
    } catch (error) {
      if (mounted) showError(context, error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final order = widget.order;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            InkWell(
              onTap: () => context.push('/pedidos/${order.id}'),
              child: Row(
                children: [
                  const IconBadge(icon: Icons.pause_circle_outline_rounded, color: BrandColors.pending),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(order.client, style: Theme.of(context).textTheme.titleMedium),
                        Text('#${order.id} · ${order.lines} productos · ${order.seller}', style: Theme.of(context).textTheme.bodySmall),
                        Text(Fmt.shortDateTime(order.date), style: Theme.of(context).textTheme.bodySmall),
                      ],
                    ),
                  ),
                  Text(Fmt.money(order.total), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                ],
              ),
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    style: OutlinedButton.styleFrom(foregroundColor: BrandColors.danger),
                    onPressed: _busy ? null : _reject,
                    child: const Text('Rechazar'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: BusyButton(label: 'Aprobar', busy: _busy, color: BrandColors.action, onPressed: _approve),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Hoja para escribir un motivo obligatorio.
class ReasonSheet extends StatefulWidget {
  const ReasonSheet({
    super.key,
    required this.title,
    required this.subtitle,
    required this.hint,
    required this.confirmLabel,
    this.danger = false,
    this.minLength = 5,
  });

  final String title;
  final String subtitle;
  final String hint;
  final String confirmLabel;
  final bool danger;
  final int minLength;

  @override
  State<ReasonSheet> createState() => _ReasonSheetState();
}

class _ReasonSheetState extends State<ReasonSheet> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final valid = _controller.text.trim().length >= widget.minLength;

    return SheetBody(
      title: widget.title,
      subtitle: widget.subtitle,
      children: [
        TextField(
          controller: _controller,
          autofocus: true,
          maxLength: 200,
          maxLines: 3,
          onChanged: (_) => setState(() {}),
          decoration: InputDecoration(hintText: widget.hint),
        ),
        const SizedBox(height: 8),
        ElevatedButton(
          style: ElevatedButton.styleFrom(backgroundColor: widget.danger ? BrandColors.danger : BrandColors.action),
          onPressed: valid ? () => Navigator.of(context).pop(_controller.text.trim()) : null,
          child: Text(widget.confirmLabel),
        ),
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
      ],
    );
  }
}
