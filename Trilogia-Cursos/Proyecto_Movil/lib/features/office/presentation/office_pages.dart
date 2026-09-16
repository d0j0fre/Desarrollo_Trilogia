import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../../auth/application/auth_controller.dart';
import '../data/office_api.dart';

final settlementsProvider = FutureProvider.autoDispose.family<List<Settlement>, String>(
  (ref, status) => ref.watch(officeApiProvider).settlements(status: status),
);
final settlementProvider = FutureProvider.autoDispose.family<Settlement, int>(
  (ref, routeId) => ref.watch(officeApiProvider).settlement(routeId),
);
final invoicesProvider = FutureProvider.autoDispose<List<Invoice>>((ref) => ref.watch(officeApiProvider).invoices());
final invoiceProvider = FutureProvider.autoDispose.family<Invoice, int>((ref, id) => ref.watch(officeApiProvider).invoice(id));

typedef _CreditQuery = ({String search, String status});
final creditsProvider = FutureProvider.autoDispose.family<List<ClientCredit>, _CreditQuery>(
  (ref, q) => ref.watch(officeApiProvider).credits(search: q.search, status: q.status),
);
final creditProvider = FutureProvider.autoDispose.family<ClientCredit, int>((ref, id) => ref.watch(officeApiProvider).credit(id));

typedef _ConsultationQuery = ({String search, String status});
final consultationsProvider = FutureProvider.autoDispose.family<List<Consultation>, _ConsultationQuery>(
  (ref, q) => ref.watch(officeApiProvider).consultations(search: q.search, status: q.status),
);
final auditProvider = FutureProvider.autoDispose.family<List<AuditEntry>, String>(
  (ref, search) => ref.watch(officeApiProvider).audit(search: search),
);

// ── Caja ─────────────────────────────────────────────────────────────────────

class SettlementsPage extends ConsumerStatefulWidget {
  const SettlementsPage({super.key});

  @override
  ConsumerState<SettlementsPage> createState() => _SettlementsPageState();
}

class _SettlementsPageState extends ConsumerState<SettlementsPage> {
  String _status = '';

  @override
  Widget build(BuildContext context) {
    final items = ref.watch(settlementsProvider(_status));

    return Scaffold(
      appBar: AppBar(title: const Text('Liquidación de rutas')),
      body: Column(
        children: [
          ListToolbar(
            filters: FilterChipsBar<String>(
              options: const ['', 'Cuadrada', 'Faltante', 'Sobrante'],
              selected: _status,
              label: (s) => s.isEmpty ? 'Todas' : s,
              onSelected: (s) => setState(() => _status = s),
            ),
          ),
          Expanded(
            child: AsyncListBody<Settlement>(
              value: items,
              onRefresh: () async => ref.invalidate(settlementsProvider(_status)),
              emptyIcon: Icons.point_of_sale_outlined,
              emptyTitle: 'Sin liquidaciones',
              emptyMessage: 'Las rutas liquidadas en el sitio web aparecen acá con su cuadre de caja.',
              itemBuilder: (context, item) {
                final balanced = item.difference == 0;
                final color = balanced ? BrandColors.delivered : item.difference > 0 ? BrandColors.inRoute : BrandColors.danger;
                return TapCard(
                  onTap: () => showModalBottomSheet<void>(
                    context: context,
                    isScrollControlled: true,
                    builder: (_) => _SettlementSheet(routeId: item.routeId),
                  ),
                  child: Row(
                    children: [
                      IconBadge(icon: balanced ? Icons.check_circle_outline_rounded : Icons.error_outline_rounded, color: color),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(item.routeCode, style: Theme.of(context).textTheme.titleMedium),
                            Text('${item.settledBy} · ${Fmt.shortDateTime(item.settledAt)}', style: Theme.of(context).textTheme.bodySmall),
                          ],
                        ),
                      ),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(Fmt.money(item.receivedCash + item.vouchersTotal), style: const TextStyle(fontWeight: FontWeight.w800)),
                          if (!balanced) Text('Dif. ${Fmt.money(item.difference)}', style: TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 13)),
                        ],
                      ),
                    ],
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _SettlementSheet extends ConsumerWidget {
  const _SettlementSheet({required this.routeId});

  final int routeId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(settlementProvider(routeId));

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
        child: data.when(
          loading: () => const Padding(padding: EdgeInsets.all(32), child: Center(child: CircularProgressIndicator())),
          error: (error, _) => InlineNotice(message: friendlyError(error)),
          data: (s) => SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(s.routeCode, style: Theme.of(context).textTheme.headlineSmall),
                Text('Estado: ${s.status}', style: Theme.of(context).textTheme.bodySmall),
                const SizedBox(height: 12),
                InfoRow(icon: Icons.payments_outlined, label: 'Efectivo esperado', value: Fmt.money(s.expectedCash)),
                InfoRow(icon: Icons.credit_card_outlined, label: 'Otros medios esperados', value: Fmt.money(s.expectedOther)),
                InfoRow(icon: Icons.savings_outlined, label: 'Efectivo recibido', value: Fmt.money(s.receivedCash)),
                InfoRow(icon: Icons.receipt_outlined, label: 'Comprobantes', value: Fmt.money(s.vouchersTotal)),
                const Divider(height: 24),
                InfoRow(icon: Icons.balance_outlined, label: 'Diferencia', value: Fmt.money(s.difference)),
                if (s.vouchers.isNotEmpty) ...[
                  const SizedBox(height: 16),
                  const SectionTitle('Comprobantes'),
                  for (final (type, reference, amount) in s.vouchers)
                    ListTile(contentPadding: EdgeInsets.zero, dense: true, title: Text(type),
                        subtitle: reference.isEmpty ? null : Text(reference), trailing: Text(Fmt.money(amount))),
                ],
                if (s.notes.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  InlineNotice(message: s.notes, color: BrandColors.textMuted, icon: Icons.notes_rounded),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

// ── Facturación ──────────────────────────────────────────────────────────────

class InvoicesPage extends ConsumerStatefulWidget {
  const InvoicesPage({super.key});

  @override
  ConsumerState<InvoicesPage> createState() => _InvoicesPageState();
}

class _InvoicesPageState extends ConsumerState<InvoicesPage> {
  String _search = '';

  @override
  Widget build(BuildContext context) {
    final search = _search.toLowerCase();
    final items = ref.watch(invoicesProvider).whenData((list) => search.isEmpty
        ? list
        : list.where((i) => i.client.toLowerCase().contains(search) || i.number.toLowerCase().contains(search) || '${i.orderId}' == search).toList());

    return Scaffold(
      appBar: AppBar(title: const Text('Facturas')),
      body: Column(
        children: [
          ListToolbar(search: AppSearchField(hint: 'Cliente, número de factura o pedido', onChanged: (v) => setState(() => _search = v))),
          Expanded(
            child: AsyncListBody<Invoice>(
              value: items,
              onRefresh: () async => ref.invalidate(invoicesProvider),
              emptyIcon: Icons.receipt_long_outlined,
              emptyTitle: 'Sin facturas',
              emptyMessage: 'No hay facturas que coincidan. Se muestran las 50 más recientes.',
              itemBuilder: (context, invoice) => TapCard(
                onTap: () => showModalBottomSheet<void>(
                  context: context,
                  isScrollControlled: true,
                  builder: (_) => _InvoiceSheet(invoiceId: invoice.id),
                ),
                child: Row(
                  children: [
                    IconBadge(icon: Icons.receipt_long_outlined, color: invoice.status == 'Generada' ? BrandColors.view : BrandColors.danger),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(invoice.client, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleMedium),
                          Text('${invoice.number} · ${Fmt.shortDateTime(invoice.date)}', style: Theme.of(context).textTheme.bodySmall),
                        ],
                      ),
                    ),
                    Text(Fmt.money(invoice.total), style: const TextStyle(fontWeight: FontWeight.w800)),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _InvoiceSheet extends ConsumerWidget {
  const _InvoiceSheet({required this.invoiceId});

  final int invoiceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(invoiceProvider(invoiceId));

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.75,
      maxChildSize: 0.95,
      builder: (context, controller) => data.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Padding(padding: const EdgeInsets.all(24), child: InlineNotice(message: friendlyError(error))),
        data: (inv) => ListView(
          controller: controller,
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
          children: [
            Text(inv.number, style: Theme.of(context).textTheme.headlineSmall),
            Text('${inv.status} · Pedido #${inv.orderId}', style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 12),
            InfoRow(icon: Icons.person_outline_rounded, label: 'Cliente', value: inv.client),
            if (inv.clientEmail.isNotEmpty) InfoRow(icon: Icons.mail_outline_rounded, label: 'Correo', value: inv.clientEmail),
            InfoRow(icon: Icons.event_outlined, label: 'Fecha', value: Fmt.dateTime(inv.date)),
            const SizedBox(height: 12),
            const SectionTitle('Detalle'),
            for (final (name, qty, price, subtotal) in inv.lines)
              ListTile(contentPadding: EdgeInsets.zero, dense: true, title: Text(name),
                  subtitle: Text('$qty × ${Fmt.money(price)}'), trailing: Text(Fmt.money(subtotal))),
            const Divider(height: 24),
            InfoRow(icon: Icons.functions_rounded, label: 'Subtotal', value: Fmt.money(inv.subtotal)),
            InfoRow(icon: Icons.percent_rounded, label: 'Impuesto', value: Fmt.money(inv.tax)),
            InfoRow(icon: Icons.payments_outlined, label: 'Total', value: Fmt.money(inv.total)),
          ],
        ),
      ),
    );
  }
}

// ── Crédito ──────────────────────────────────────────────────────────────────

class CreditPage extends ConsumerStatefulWidget {
  const CreditPage({super.key});

  @override
  ConsumerState<CreditPage> createState() => _CreditPageState();
}

class _CreditPageState extends ConsumerState<CreditPage> {
  String _search = '';
  String _status = '';

  _CreditQuery get _q => (search: _search, status: _status);

  @override
  Widget build(BuildContext context) {
    final items = ref.watch(creditsProvider(_q));

    return Scaffold(
      appBar: AppBar(title: const Text('Crédito de clientes')),
      body: Column(
        children: [
          ListToolbar(
            search: AppSearchField(hint: 'Buscar cliente', onChanged: (v) => setState(() => _search = v)),
            filters: FilterChipsBar<String>(
              options: const ['', 'ConDeuda', 'Bloqueado', 'Activo', 'SinCredito'],
              selected: _status,
              label: (s) => switch (s) {
                'ConDeuda' => 'Con deuda',
                'Bloqueado' => 'Bloqueados',
                'Activo' => 'Crédito activo',
                'SinCredito' => 'Sin crédito',
                _ => 'Todos',
              },
              onSelected: (s) => setState(() => _status = s),
            ),
          ),
          Expanded(
            child: AsyncListBody<ClientCredit>(
              value: items,
              onRefresh: () async => ref.invalidate(creditsProvider(_q)),
              emptyIcon: Icons.account_balance_wallet_outlined,
              emptyTitle: 'Sin clientes',
              emptyMessage: 'No hay clientes con este filtro.',
              itemBuilder: (context, c) {
                final color = c.blocked ? BrandColors.danger : c.debt > 0 ? BrandColors.pending : BrandColors.delivered;
                return TapCard(
                  onTap: () => showModalBottomSheet<void>(
                    context: context,
                    isScrollControlled: true,
                    builder: (_) => _CreditSheet(clientId: c.id),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          Expanded(child: Text(c.name, style: Theme.of(context).textTheme.titleMedium)),
                          StatusChip(label: c.statusLabel, color: color),
                        ],
                      ),
                      const SizedBox(height: 10),
                      Row(
                        children: [
                          _Amount(label: 'Deuda', value: c.debt, color: c.debt > 0 ? BrandColors.pending : BrandColors.black),
                          _Amount(label: 'Disponible', value: c.available, color: BrandColors.delivered),
                          _Amount(label: 'Límite', value: c.limit),
                        ],
                      ),
                      if (c.limit > 0) ...[
                        const SizedBox(height: 10),
                        ClipRRect(
                          borderRadius: BorderRadius.circular(99),
                          child: LinearProgressIndicator(value: (c.debt / c.limit).clamp(0, 1), minHeight: 8, color: color),
                        ),
                      ],
                    ],
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _Amount extends StatelessWidget {
  const _Amount({required this.label, required this.value, this.color = BrandColors.black});

  final String label;
  final double value;
  final Color color;

  @override
  Widget build(BuildContext context) => Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            FittedBox(fit: BoxFit.scaleDown, child: Text(Fmt.moneyCompact(value), style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: color))),
            Text(label, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      );
}

class _CreditSheet extends ConsumerWidget {
  const _CreditSheet({required this.clientId});

  final int clientId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(creditProvider(clientId));

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.8,
      maxChildSize: 0.95,
      builder: (context, controller) => data.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Padding(padding: const EdgeInsets.all(24), child: InlineNotice(message: friendlyError(error))),
        data: (c) => ListView(
          controller: controller,
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
          children: [
            Text(c.name, style: Theme.of(context).textTheme.headlineSmall),
            Text([c.email, c.phone].where((t) => t.isNotEmpty).join(' · '), style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 14),
            Row(children: [
              Expanded(child: MetricTile(label: 'Deuda actual', value: Fmt.moneyCompact(c.debt), icon: Icons.trending_up_rounded, color: BrandColors.pending)),
              const SizedBox(width: 10),
              Expanded(child: MetricTile(label: 'Disponible', value: Fmt.moneyCompact(c.available), icon: Icons.account_balance_wallet_outlined, color: BrandColors.delivered)),
            ]),
            const SizedBox(height: 8),
            InfoRow(icon: Icons.speed_rounded, label: 'Límite de crédito', value: Fmt.money(c.limit)),
            InfoRow(icon: Icons.add_card_outlined, label: 'Total de cargos', value: Fmt.money(c.charges)),
            InfoRow(icon: Icons.payments_outlined, label: 'Total de abonos', value: Fmt.money(c.payments)),
            if (c.blocked && c.blockReason.isNotEmpty) ...[
              const SizedBox(height: 8),
              InlineNotice(message: 'Bloqueado: ${c.blockReason}'),
            ],
            const SizedBox(height: 16),
            const SectionTitle('Movimientos'),
            if (c.movements.isEmpty) Text('Sin movimientos.', style: Theme.of(context).textTheme.bodySmall),
            for (final m in c.movements)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: IconBadge(
                  icon: m.increasesDebt ? Icons.north_east_rounded : Icons.south_west_rounded,
                  color: m.increasesDebt ? BrandColors.pending : BrandColors.delivered,
                  size: 36,
                ),
                title: Text('${m.type} · ${Fmt.money(m.amount)}', style: const TextStyle(fontWeight: FontWeight.w600)),
                subtitle: Text([m.description, Fmt.shortDateTime(m.date)].where((t) => t.isNotEmpty).join(' · ')),
              ),
          ],
        ),
      ),
    );
  }
}

// ── Soporte ──────────────────────────────────────────────────────────────────

class SupportPage extends ConsumerStatefulWidget {
  const SupportPage({super.key});

  @override
  ConsumerState<SupportPage> createState() => _SupportPageState();
}

class _SupportPageState extends ConsumerState<SupportPage> {
  String _search = '';
  String _status = 'Pendiente';

  _ConsultationQuery get _q => (search: _search, status: _status);

  @override
  Widget build(BuildContext context) {
    final items = ref.watch(consultationsProvider(_q));

    return Scaffold(
      appBar: AppBar(title: const Text('Consultas de clientes')),
      body: Column(
        children: [
          ListToolbar(
            search: AppSearchField(hint: 'Nombre, correo o asunto', onChanged: (v) => setState(() => _search = v)),
            filters: FilterChipsBar<String>(
              options: const ['Pendiente', 'Atendida', 'Cerrada', ''],
              selected: _status,
              label: (s) => s.isEmpty ? 'Todas' : '${s}s',
              onSelected: (s) => setState(() => _status = s),
            ),
          ),
          Expanded(
            child: AsyncListBody<Consultation>(
              value: items,
              onRefresh: () async => ref.invalidate(consultationsProvider(_q)),
              emptyIcon: Icons.mark_email_read_outlined,
              emptyTitle: 'Sin consultas',
              emptyMessage: _status == 'Pendiente' ? 'No hay consultas esperando respuesta.' : 'No hay consultas con este filtro.',
              itemBuilder: (context, c) => TapCard(
                onTap: () async {
                  final updated = await showModalBottomSheet<bool>(
                    context: context,
                    isScrollControlled: true,
                    builder: (_) => _ConsultationSheet(consultation: c),
                  );
                  if (updated == true) ref.invalidate(consultationsProvider);
                },
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(child: Text(c.subject, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleMedium)),
                        StatusChip(label: c.status, color: _consultationColor(c.status)),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text('${c.name} · ${Fmt.shortDateTime(c.createdAt)}', style: Theme.of(context).textTheme.bodySmall),
                    const SizedBox(height: 6),
                    Text(c.message, maxLines: 2, overflow: TextOverflow.ellipsis),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

Color _consultationColor(String status) => switch (status) {
      'Atendida' => BrandColors.inRoute,
      'Cerrada' => BrandColors.delivered,
      _ => BrandColors.pending,
    };

class _ConsultationSheet extends ConsumerStatefulWidget {
  const _ConsultationSheet({required this.consultation});

  final Consultation consultation;

  @override
  ConsumerState<_ConsultationSheet> createState() => _ConsultationSheetState();
}

class _ConsultationSheetState extends ConsumerState<_ConsultationSheet> {
  late String _status = widget.consultation.status;
  late final _response = TextEditingController(text: widget.consultation.internalResponse);
  bool _saving = false;

  @override
  void dispose() {
    _response.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      await ref.read(officeApiProvider).updateConsultation(widget.consultation.id, _status, _response.text.trim());
      if (mounted) {
        Navigator.of(context).pop(true);
        showSuccess(context, 'Consulta actualizada.');
      }
    } catch (error) {
      if (mounted) {
        setState(() => _saving = false);
        showError(context, error);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = widget.consultation;
    final canAttend = ref.watch(sessionProvider)?.puede('CONSULTAS_ATENDER') ?? false;

    return SheetBody(
      title: c.subject,
      subtitle: '${c.name} · ${c.email}\n${Fmt.dateTime(c.createdAt)}',
      children: [
        Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(color: BrandColors.surface, borderRadius: BorderRadius.circular(AppTheme.radiusSmall)),
          child: Text(c.message, style: Theme.of(context).textTheme.bodyLarge),
        ),
        if (c.attendedBy.isNotEmpty) ...[
          const SizedBox(height: 10),
          Text('Atendida por ${c.attendedBy} · ${Fmt.shortDateTime(c.attendedAt)}', style: Theme.of(context).textTheme.bodySmall),
        ],
        const SizedBox(height: 16),
        if (canAttend) ...[
          SegmentedButton<String>(
            segments: const [
              ButtonSegment(value: 'Pendiente', label: Text('Pendiente')),
              ButtonSegment(value: 'Atendida', label: Text('Atendida')),
              ButtonSegment(value: 'Cerrada', label: Text('Cerrada')),
            ],
            selected: {_status},
            showSelectedIcon: false,
            style: SegmentedButton.styleFrom(
              selectedBackgroundColor: BrandColors.redPrimary,
              selectedForegroundColor: BrandColors.white,
              side: const BorderSide(color: BrandColors.border),
            ),
            onSelectionChanged: (v) => setState(() => _status = v.first),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _response,
            maxLength: 1000,
            maxLines: 3,
            decoration: const InputDecoration(labelText: 'Nota interna', hintText: 'Qué se respondió o qué falta'),
          ),
          BusyButton(label: 'Guardar', icon: Icons.save_outlined, busy: _saving, color: BrandColors.action, onPressed: _save),
        ] else if (c.internalResponse.isNotEmpty)
          InlineNotice(message: c.internalResponse, color: BrandColors.inRoute, icon: Icons.forum_outlined),
      ],
    );
  }
}

// ── Auditoría ────────────────────────────────────────────────────────────────

class AuditPage extends ConsumerStatefulWidget {
  const AuditPage({super.key});

  @override
  ConsumerState<AuditPage> createState() => _AuditPageState();
}

class _AuditPageState extends ConsumerState<AuditPage> {
  String _search = '';
  bool _mobileOnly = false;

  @override
  Widget build(BuildContext context) {
    final items = ref.watch(auditProvider(_search)).whenData(
          (list) => _mobileOnly ? list.where((e) => e.fromMobile).toList() : list,
        );

    return Scaffold(
      appBar: AppBar(title: const Text('Bitácora')),
      body: Column(
        children: [
          ListToolbar(
            search: AppSearchField(hint: 'Usuario, acción, módulo o detalle', onChanged: (v) => setState(() => _search = v)),
            filters: FilterChipsBar<bool>(
              options: const [false, true],
              selected: _mobileOnly,
              label: (m) => m ? 'Desde la aplicación' : 'Todo',
              onSelected: (m) => setState(() => _mobileOnly = m),
            ),
          ),
          Expanded(
            child: AsyncListBody<AuditEntry>(
              value: items,
              onRefresh: () async => ref.invalidate(auditProvider(_search)),
              spacing: 8,
              emptyIcon: Icons.history_rounded,
              emptyTitle: 'Sin registros',
              emptyMessage: 'No hay acciones que coincidan con la búsqueda.',
              itemBuilder: (context, e) => Card(
                child: Padding(
                  padding: const EdgeInsets.all(14),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      IconBadge(icon: e.fromMobile ? Icons.smartphone_rounded : Icons.computer_rounded,
                          color: e.fromMobile ? BrandColors.redPrimary : BrandColors.inRoute, size: 38),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('${e.action} · ${e.module}', style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                            const SizedBox(height: 2),
                            Text(e.description, maxLines: 3, overflow: TextOverflow.ellipsis),
                            const SizedBox(height: 4),
                            Text('${e.user} (${e.role}) · ${Fmt.shortDateTime(e.date)}', style: Theme.of(context).textTheme.bodySmall),
                          ],
                        ),
                      ),
                    ],
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
