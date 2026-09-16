import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../data/management_api.dart';

final managedRoutesProvider = FutureProvider.autoDispose.family<List<ManagedRoute>, String>(
  (ref, status) => ref.watch(managementApiProvider).routes(status: status),
);

final managedRouteProvider = FutureProvider.autoDispose.family<ManagedRouteDetail, int>(
  (ref, id) => ref.watch(managementApiProvider).route(id),
);

Color routeStatusColor(String status) => switch (status) {
      'Despachada' => BrandColors.inRoute,
      'Planificada' => BrandColors.pending,
      'Completada' => BrandColors.delivered,
      'Cancelada' => BrandColors.danger,
      _ => BrandColors.textMuted,
    };

/// Rutas de todos los choferes: seguimiento, despacho y reasignación.
class ManagedRoutesPage extends ConsumerStatefulWidget {
  const ManagedRoutesPage({super.key});

  @override
  ConsumerState<ManagedRoutesPage> createState() => _ManagedRoutesPageState();
}

class _ManagedRoutesPageState extends ConsumerState<ManagedRoutesPage> {
  static const _statuses = ['Despachada', 'Planificada', 'Completada', ''];
  String _status = 'Despachada';

  @override
  Widget build(BuildContext context) {
    final routes = ref.watch(managedRoutesProvider(_status));

    return Scaffold(
      appBar: AppBar(title: const Text('Rutas y choferes')),
      body: Column(
        children: [
          ListToolbar(
            filters: FilterChipsBar<String>(
              options: _statuses,
              selected: _status,
              label: (s) => switch (s) {
                'Despachada' => 'En calle',
                'Planificada' => 'Planificadas',
                'Completada' => 'Completadas',
                _ => 'Todas',
              },
              onSelected: (s) => setState(() => _status = s),
            ),
          ),
          Expanded(
            child: AsyncListBody<ManagedRoute>(
              value: routes,
              onRefresh: () async => ref.invalidate(managedRoutesProvider(_status)),
              emptyIcon: Icons.route_outlined,
              emptyTitle: 'Sin rutas',
              emptyMessage: _status == 'Despachada' ? 'No hay rutas en calle en este momento.' : 'No hay rutas con este filtro.',
              itemBuilder: (context, route) => TapCard(
                onTap: () => context.push('/gestion/rutas/${route.id}'),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        IconBadge(icon: Icons.local_shipping_outlined, color: routeStatusColor(route.status)),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(route.code, style: Theme.of(context).textTheme.titleMedium),
                              Text(route.zone, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall),
                            ],
                          ),
                        ),
                        StatusChip(label: route.status, color: routeStatusColor(route.status)),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 14,
                      runSpacing: 6,
                      children: [
                        _Meta(icon: Icons.person_outline_rounded, text: route.driver),
                        _Meta(icon: Icons.directions_car_outlined, text: route.plate),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(child: Text('${route.delivered + route.failed} de ${route.total} paradas atendidas',
                            style: const TextStyle(fontWeight: FontWeight.w600))),
                        if (route.failed > 0) StatusChip(label: '${route.failed} fallidas', color: BrandColors.danger),
                      ],
                    ),
                    const SizedBox(height: 8),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(99),
                      child: LinearProgressIndicator(value: route.progress, minHeight: 8, color: BrandColors.delivered),
                    ),
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

class _Meta extends StatelessWidget {
  const _Meta({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 17, color: BrandColors.textMuted),
          const SizedBox(width: 5),
          Text(text, style: Theme.of(context).textTheme.bodyMedium),
        ],
      );
}

class ManagedRouteDetailPage extends ConsumerStatefulWidget {
  const ManagedRouteDetailPage({super.key, required this.routeId});

  final int routeId;

  @override
  ConsumerState<ManagedRouteDetailPage> createState() => _ManagedRouteDetailPageState();
}

class _ManagedRouteDetailPageState extends ConsumerState<ManagedRouteDetailPage> {
  bool _busy = false;

  void _refresh() {
    ref.invalidate(managedRouteProvider(widget.routeId));
    ref.invalidate(managedRoutesProvider);
  }

  Future<void> _dispatch(ManagedRouteDetail route) async {
    final confirmed = await confirmSheet(
      context,
      title: 'Despachar ruta',
      message: '${route.code} sale a calle con ${route.driver} en ${route.plate}. '
          'Las ${route.stops.length} paradas pasan a "En ruta" y cada cliente recibe un aviso por correo.',
      confirmLabel: 'Despachar',
      icon: Icons.local_shipping_rounded,
    );
    if (!confirmed || !mounted) return;

    setState(() => _busy = true);
    try {
      final count = await ref.read(managementApiProvider).dispatch(route.id);
      _refresh();
      if (mounted) showSuccess(context, 'Ruta despachada: $count pedidos en ruta.');
    } catch (error) {
      if (mounted) showError(context, error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _reassign(ManagedRouteDetail route) async {
    final done = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _ReassignSheet(route: route),
    );
    if (done == true) {
      _refresh();
      if (mounted) showSuccess(context, 'Ruta reasignada. El chofer nuevo la ve al actualizar su lista.');
    }
  }

  @override
  Widget build(BuildContext context) {
    final route = ref.watch(managedRouteProvider(widget.routeId));

    return Scaffold(
      appBar: AppBar(title: Text(route.value?.code ?? 'Ruta')),
      body: AsyncDetailBody<ManagedRouteDetail>(
        value: route,
        onRefresh: () async => _refresh(),
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
                        Expanded(child: Text(detail.zone, style: Theme.of(context).textTheme.titleLarge)),
                        StatusChip(label: detail.status, color: routeStatusColor(detail.status)),
                      ],
                    ),
                    const SizedBox(height: 6),
                    InfoRow(icon: Icons.person_outline_rounded, label: 'Chofer', value: detail.driver),
                    InfoRow(icon: Icons.directions_car_outlined, label: 'Vehículo',
                        value: detail.vehicleDescription.isEmpty ? detail.plate : '${detail.plate} · ${detail.vehicleDescription}'),
                    if (detail.dispatchedAt != null)
                      InfoRow(icon: Icons.schedule_rounded, label: 'Despachada', value: Fmt.dateTime(detail.dispatchedAt)),
                    if (detail.notes.isNotEmpty) ...[
                      const SizedBox(height: 6),
                      InlineNotice(message: detail.notes, color: BrandColors.textMuted, icon: Icons.notes_rounded),
                    ],
                    if (detail.canDispatch || detail.canReassign) const SizedBox(height: 14),
                    if (detail.canDispatch) ...[
                      BusyButton(label: 'Despachar ruta', icon: Icons.local_shipping_rounded, busy: _busy,
                          color: BrandColors.action, onPressed: () => _dispatch(detail)),
                      const SizedBox(height: 10),
                    ],
                    if (detail.canReassign)
                      OutlinedButton.icon(
                        style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(AppTheme.buttonHeight)),
                        onPressed: _busy ? null : () => _reassign(detail),
                        icon: const Icon(Icons.swap_horiz_rounded),
                        label: const Text('Reasignar chofer o vehículo'),
                      ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 20),
            SectionTitle('Paradas', trailing: Text('${detail.stops.length}', style: Theme.of(context).textTheme.titleSmall)),
            if (detail.stops.isEmpty)
              const Card(child: Padding(padding: EdgeInsets.all(16), child: Text('La ruta no tiene paradas.')))
            else
              Card(
                child: Column(
                  children: [
                    for (var i = 0; i < detail.stops.length; i++) ...[
                      if (i > 0) const Divider(indent: 16, endIndent: 16),
                      _StopTile(stop: detail.stops[i]),
                    ],
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _StopTile extends StatelessWidget {
  const _StopTile({required this.stop});

  final ManagedStop stop;

  @override
  Widget build(BuildContext context) {
    final color = AppTheme.deliveryStatusColor(stop.status);

    return ListTile(
      onTap: () => context.push('/pedidos/${stop.orderId}'),
      leading: CircleAvatar(
        backgroundColor: color.withValues(alpha: 0.12),
        child: Text('${stop.sequence}', style: TextStyle(color: color, fontWeight: FontWeight.w800)),
      ),
      title: Text(stop.client, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w600)),
      subtitle: Text(
        stop.failureReason.isNotEmpty ? 'Motivo: ${stop.failureReason}' : stop.address,
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: StatusChip(label: AppTheme.deliveryStatusLabel(stop.status), color: color),
    );
  }
}

final _driversProvider = FutureProvider.autoDispose<List<DriverOption>>((ref) => ref.watch(managementApiProvider).drivers());
final _vehiclesProvider = FutureProvider.autoDispose<List<VehicleOption>>((ref) => ref.watch(managementApiProvider).vehicles());

class _ReassignSheet extends ConsumerStatefulWidget {
  const _ReassignSheet({required this.route});

  final ManagedRouteDetail route;

  @override
  ConsumerState<_ReassignSheet> createState() => _ReassignSheetState();
}

class _ReassignSheetState extends ConsumerState<_ReassignSheet> {
  late int _driverId = widget.route.driverId;
  late int _vehicleId = widget.route.vehicleId;
  final _reason = TextEditingController();
  bool _busy = false;

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  bool get _changed => _driverId != widget.route.driverId || _vehicleId != widget.route.vehicleId;
  bool get _valid => _changed && _reason.text.trim().length >= 5;

  Future<void> _save() async {
    setState(() => _busy = true);
    try {
      await ref.read(managementApiProvider).reassign(
            routeId: widget.route.id,
            driverId: _driverId,
            vehicleId: _vehicleId == widget.route.vehicleId ? null : _vehicleId,
            reason: _reason.text.trim(),
          );
      if (mounted) Navigator.of(context).pop(true);
    } catch (error) {
      if (mounted) {
        setState(() => _busy = false);
        showError(context, error);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final drivers = ref.watch(_driversProvider);
    final vehicles = ref.watch(_vehiclesProvider);

    return SheetBody(
      title: 'Reasignar ruta',
      subtitle: widget.route.status == 'Despachada'
          ? 'La ruta ya está en calle. Las entregas cerradas conservan su historia; las pendientes pasan al chofer nuevo.'
          : 'Elegí quién sale con esta ruta.',
      children: [
        drivers.when(
          loading: () => const LinearProgressIndicator(),
          error: (error, _) => InlineNotice(message: friendlyError(error)),
          data: (items) => DropdownButtonFormField<int>(
            initialValue: items.any((d) => d.id == _driverId) ? _driverId : null,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Chofer', prefixIcon: Icon(Icons.person_outline_rounded)),
            items: [
              for (final driver in items)
                DropdownMenuItem(
                  value: driver.id,
                  child: Text(
                    '${driver.name}${driver.id == widget.route.driverId ? ' (actual)' : ' · ${driver.openRoutes} rutas abiertas'}',
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
            ],
            onChanged: (value) => setState(() => _driverId = value ?? _driverId),
          ),
        ),
        const SizedBox(height: 14),
        vehicles.when(
          loading: () => const LinearProgressIndicator(),
          error: (error, _) => InlineNotice(message: friendlyError(error)),
          data: (items) => DropdownButtonFormField<int>(
            initialValue: items.any((v) => v.id == _vehicleId) ? _vehicleId : null,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Vehículo', prefixIcon: Icon(Icons.directions_car_outlined)),
            items: [
              for (final vehicle in items)
                DropdownMenuItem(
                  value: vehicle.id,
                  child: Text(
                    '${vehicle.plate}${vehicle.id == widget.route.vehicleId ? ' (actual)' : ' · ${vehicle.openRoutes} rutas abiertas'}',
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
            ],
            onChanged: (value) => setState(() => _vehicleId = value ?? _vehicleId),
          ),
        ),
        const SizedBox(height: 14),
        TextField(
          controller: _reason,
          maxLength: 200,
          maxLines: 2,
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(labelText: 'Motivo', hintText: 'Ej.: el chofer se reportó enfermo'),
        ),
        if (!_changed)
          Text('Elegí otro chofer o vehículo para continuar.', style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(height: 12),
        BusyButton(label: 'Reasignar', icon: Icons.swap_horiz_rounded, busy: _busy, color: BrandColors.action,
            onPressed: _valid ? _save : null),
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
      ],
    );
  }
}
