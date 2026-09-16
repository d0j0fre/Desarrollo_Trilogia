import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../../auth/application/auth_controller.dart';
import '../data/management_api.dart';

final dashboardProvider = FutureProvider.autoDispose.family<Dashboard, String>(
  (ref, range) => ref.watch(managementApiProvider).dashboard(range),
);

/// Métricas del negocio.
///
/// Rangos fijos a propósito: en el teléfono no se arma un reporte, se mira
/// cómo va el día, la semana o el mes. El reporte detallado sigue en la web.
class DashboardPage extends ConsumerStatefulWidget {
  const DashboardPage({super.key});

  @override
  ConsumerState<DashboardPage> createState() => _DashboardPageState();
}

class _DashboardPageState extends ConsumerState<DashboardPage> {
  static const _ranges = ['hoy', 'semana', 'mes', '30dias'];
  String _range = 'hoy';

  @override
  Widget build(BuildContext context) {
    final dashboard = ref.watch(dashboardProvider(_range));

    return Scaffold(
      appBar: AppBar(title: const Text('Métricas del negocio')),
      body: Column(
        children: [
          ListToolbar(
            filters: FilterChipsBar<String>(
              options: _ranges,
              selected: _range,
              label: (range) => switch (range) {
                'hoy' => 'Hoy',
                'semana' => 'Últimos 7 días',
                'mes' => 'Este mes',
                _ => 'Últimos 30 días',
              },
              onSelected: (range) => setState(() => _range = range),
            ),
          ),
          Expanded(
            child: AsyncDetailBody<Dashboard>(
              value: dashboard,
              onRefresh: () async => ref.invalidate(dashboardProvider(_range)),
              builder: (context, data) => _DashboardBody(data: data, range: _range),
            ),
          ),
        ],
      ),
    );
  }
}

class _DashboardBody extends ConsumerWidget {
  const _DashboardBody({required this.data, required this.range});

  final Dashboard data;
  final String range;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionProvider);
    final canApprove = session?.puede('PEDIDOS_AUTORIZAR_RECHAZAR') ?? false;
    final canRoutes = session?.puede('RUTAS_GESTIONAR') ?? false;
    final period = data.from == null
        ? ''
        : data.from == data.to
            ? DateFormat("EEEE d 'de' MMMM", 'es').format(data.from!)
            : '${Fmt.date(data.from)} al ${Fmt.date(data.to)}';

    Widget grid(List<Widget> tiles) => Column(
          children: [
            for (var i = 0; i < tiles.length; i += 2)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: IntrinsicHeight(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Expanded(child: tiles[i]),
                      const SizedBox(width: 10),
                      Expanded(child: i + 1 < tiles.length ? tiles[i + 1] : const SizedBox()),
                    ],
                  ),
                ),
              ),
          ],
        );

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
      children: [
        if (period.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(left: 4, bottom: 12),
            child: Text(period[0].toUpperCase() + period.substring(1), style: Theme.of(context).textTheme.bodySmall),
          ),
        _SalesHero(data: data, range: range),
        const SizedBox(height: 12),
        grid([
          MetricTile(label: 'Facturas emitidas', value: Fmt.number(data.invoices), icon: Icons.receipt_long_outlined, color: BrandColors.view),
          MetricTile(label: 'Ticket promedio', value: Fmt.moneyCompact(data.averageTicket), icon: Icons.sell_outlined, color: BrandColors.inRoute),
          MetricTile(label: 'Pedidos recibidos', value: Fmt.number(data.orders), icon: Icons.shopping_bag_outlined, color: BrandColors.redPrimary),
          MetricTile(label: 'Cobros pendientes', value: Fmt.moneyCompact(data.receivables), icon: Icons.account_balance_wallet_outlined, color: BrandColors.pending, caption: 'Saldo de crédito'),
        ]),
        const SizedBox(height: 12),
        const SectionTitle('Operación'),
        Card(
          child: Column(
            children: [
              _OperationRow(icon: Icons.local_shipping_outlined, color: BrandColors.inRoute, label: 'Rutas en calle',
                  value: '${data.dispatchedRoutes}', detail: '${data.plannedRoutes} ${data.plannedRoutes == 1 ? 'planificada' : 'planificadas'}',
                  onTap: canRoutes ? () => context.push('/gestion/rutas') : null),
              const Divider(indent: 16, endIndent: 16),
              _OperationRow(icon: Icons.schedule_rounded, color: BrandColors.pending, label: 'Entregas pendientes', value: '${data.pendingDeliveries}'),
              const Divider(indent: 16, endIndent: 16),
              _OperationRow(icon: Icons.check_circle_outline_rounded, color: BrandColors.delivered, label: 'Entregadas en el período',
                  value: '${data.completedDeliveries}', detail: data.failedDeliveries > 0 ? '${data.failedDeliveries} no ${data.failedDeliveries == 1 ? 'entregada' : 'entregadas'}' : null),
              const Divider(indent: 16, endIndent: 16),
              _OperationRow(icon: Icons.pause_circle_outline_rounded, color: data.retainedOrders > 0 ? BrandColors.danger : BrandColors.textMuted,
                  label: 'Pedidos retenidos', value: '${data.retainedOrders}',
                  detail: data.retainedOrders > 0 ? 'Esperan aprobación' : null,
                  onTap: canApprove && data.retainedOrders > 0 ? () => context.push('/gestion/retenidos') : null),
            ],
          ),
        ),
        if (data.ordersByStatus.isNotEmpty) ...[
          const SizedBox(height: 20),
          const SectionTitle('Pedidos abiertos por estado'),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: _StatusBars(items: data.ordersByStatus),
            ),
          ),
        ],
        const SizedBox(height: 20),
        const SectionTitle('Más vendidos'),
        Card(
          child: data.topProducts.isEmpty
              ? const Padding(padding: EdgeInsets.all(16), child: Text('Sin ventas facturadas en el período.'))
              : Column(
                  children: [
                    for (var i = 0; i < data.topProducts.length; i++)
                      ListTile(
                        leading: CircleAvatar(
                          radius: 16,
                          backgroundColor: BrandColors.redPrimary.withValues(alpha: 0.1),
                          child: Text('${i + 1}', style: const TextStyle(color: BrandColors.redPrimary, fontWeight: FontWeight.w800)),
                        ),
                        title: Text(data.topProducts[i].name, maxLines: 1, overflow: TextOverflow.ellipsis),
                        subtitle: Text('${Fmt.number(data.topProducts[i].units)} unidades'),
                        trailing: Text(Fmt.moneyCompact(data.topProducts[i].amount), style: const TextStyle(fontWeight: FontWeight.w700)),
                      ),
                  ],
                ),
        ),
        const SizedBox(height: 20),
        SectionTitle('Existencias en riesgo',
            trailing: Text('${data.outOfStock} ${data.outOfStock == 1 ? "agotado" : "agotados"} · ${data.lowStock} con stock bajo', style: Theme.of(context).textTheme.bodySmall)),
        Card(
          child: data.stockRisk.isEmpty
              ? const Padding(padding: EdgeInsets.all(16), child: Text('Todo el catálogo activo tiene stock suficiente.'))
              : Column(
                  children: [
                    for (final (name, stock, status) in data.stockRisk)
                      ListTile(
                        leading: Icon(status == 'Agotado' ? Icons.error_outline_rounded : Icons.warning_amber_rounded,
                            color: status == 'Agotado' ? BrandColors.danger : BrandColors.pending),
                        title: Text(name, maxLines: 1, overflow: TextOverflow.ellipsis),
                        trailing: StatusChip(label: status == 'Agotado' ? 'Agotado' : '$stock unid.',
                            color: status == 'Agotado' ? BrandColors.danger : BrandColors.pending),
                      ),
                  ],
                ),
        ),
      ],
    );
  }
}

class _SalesHero extends StatelessWidget {
  const _SalesHero({required this.data, required this.range});

  final Dashboard data;
  final String range;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(gradient: BrandColors.headerGradient, borderRadius: BorderRadius.circular(AppTheme.radius)),
      padding: const EdgeInsets.all(18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Ventas facturadas', style: TextStyle(color: BrandColors.goldLight, fontWeight: FontWeight.w600)),
          const SizedBox(height: 6),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(Fmt.money(data.sales),
                style: const TextStyle(color: BrandColors.white, fontSize: 32, fontWeight: FontWeight.w800, letterSpacing: -0.6)),
          ),
          if (range != 'hoy' && data.series.isNotEmpty) ...[
            const SizedBox(height: 16),
            SizedBox(height: 90, child: _SalesChart(points: data.series, from: data.from, to: data.to)),
          ],
        ],
      ),
    );
  }
}

/// Barras diarias. Se completan los días sin ventas en cero: un hueco en el
/// gráfico se lee como falta de datos, no como un día flojo.
class _SalesChart extends StatelessWidget {
  const _SalesChart({required this.points, required this.from, required this.to});

  final List<SalesPoint> points;
  final DateTime? from;
  final DateTime? to;

  @override
  Widget build(BuildContext context) {
    final start = from ?? points.first.day;
    final end = to ?? points.last.day;
    final days = end.difference(start).inDays + 1;
    final byDay = {for (final p in points) DateUtils.dateOnly(p.day): p.total};
    final values = [for (var i = 0; i < days; i++) byDay[DateUtils.dateOnly(start.add(Duration(days: i)))] ?? 0.0];
    final maxValue = values.fold<double>(0, math.max);

    return Semantics(
      label: 'Gráfico de ventas por día',
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          for (final value in values)
            Expanded(
              child: Padding(
                padding: EdgeInsets.symmetric(horizontal: days > 20 ? 1 : 3),
                child: FractionallySizedBox(
                  heightFactor: maxValue == 0 ? 0.04 : math.max(0.04, value / maxValue),
                  alignment: Alignment.bottomCenter,
                  child: Container(
                    decoration: BoxDecoration(
                      color: value == 0 ? BrandColors.white.withValues(alpha: 0.18) : BrandColors.goldLight,
                      borderRadius: BorderRadius.circular(4),
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

class _OperationRow extends StatelessWidget {
  const _OperationRow({required this.icon, required this.color, required this.label, required this.value, this.detail, this.onTap});

  final IconData icon;
  final Color color;
  final String label;
  final String value;
  final String? detail;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Row(
          children: [
            IconBadge(icon: icon, color: color, size: 40),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15)),
                  if (detail != null) Text(detail!, style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ),
            Text(value, style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800, color: color)),
            if (onTap != null) const Icon(Icons.chevron_right_rounded, color: BrandColors.textMuted),
          ],
        ),
      ),
    );
  }
}

class _StatusBars extends StatelessWidget {
  const _StatusBars({required this.items});

  final List<(String, int)> items;

  @override
  Widget build(BuildContext context) {
    final total = items.fold<int>(0, (sum, item) => sum + item.$2);

    return Column(
      children: [
        for (final (status, count) in items)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Row(
              children: [
                SizedBox(width: 104, child: Text(Fmt.status(status), style: const TextStyle(fontWeight: FontWeight.w600))),
                Expanded(
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(99),
                    child: LinearProgressIndicator(
                      value: total == 0 ? 0 : count / total,
                      minHeight: 10,
                      color: OrderStatusStyle.color(status),
                      backgroundColor: BrandColors.surfaceAlt,
                    ),
                  ),
                ),
                SizedBox(width: 36, child: Text('$count', textAlign: TextAlign.end, style: const TextStyle(fontWeight: FontWeight.w800))),
              ],
            ),
          ),
      ],
    );
  }
}
