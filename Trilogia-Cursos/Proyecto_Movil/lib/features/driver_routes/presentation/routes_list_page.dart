import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../application/routes_controller.dart';
import '../domain/route.dart';

class RoutesListPage extends ConsumerWidget {
  const RoutesListPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final routes = ref.watch(routesControllerProvider);

    Widget llenarPantalla(Widget child) => LayoutBuilder(
      builder: (context, constraints) => ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        children: [
          ConstrainedBox(
            constraints: BoxConstraints(minHeight: constraints.maxHeight),
            child: child,
          ),
        ],
      ),
    );

    return Scaffold(
      appBar: AppBar(
        title: const Text('Mis rutas'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Actualizar',
            onPressed: routes.isLoading
                ? null
                : () => ref.read(routesControllerProvider.notifier).refresh(),
          ),
        ],
      ),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () =>
                  ref.read(routesControllerProvider.notifier).refresh(),
              child: routes.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (error, _) => llenarPantalla(
                  ErrorView(
                    message:
                        'No se pudieron traer las rutas y no hay nada guardado en el teléfono.',
                    onRetry: () =>
                        ref.read(routesControllerProvider.notifier).refresh(),
                  ),
                ),
                data: (data) => data.isEmpty
                    ? llenarPantalla(
                        const EmptyState(
                          icon: Icons.route_rounded,
                          title: 'No tenés rutas asignadas',
                          message:
                              'Cuando operaciones te asigne una ruta va a aparecer acá.\nDeslizá hacia abajo para revisar de nuevo.',
                        ),
                      )
                    : ListView.separated(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
                        itemCount: data.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 12),
                        itemBuilder: (context, index) =>
                            _RouteCard(route: data[index]),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _RouteCard extends StatelessWidget {
  const _RouteCard({required this.route});

  final DriverRoute route;

  @override
  Widget build(BuildContext context) {
    final esActiva = route.estaActiva;
    final porcentaje = (route.avance * 100).round();
    final theme = Theme.of(context);

    return Card(
      child: InkWell(
        onTap: () => context.push('/rutas/${route.rutaId}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  IconBadge(
                    icon: Icons.route_rounded,
                    color: esActiva
                        ? BrandColors.redPrimary
                        : BrandColors.textMuted,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(route.codigo, style: theme.textTheme.titleMedium),
                        if (route.zona.isNotEmpty)
                          Text(
                            route.zona,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: theme.textTheme.bodySmall,
                          ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
                  StatusChip(
                    label: route.estado,
                    color: esActiva
                        ? BrandColors.inRoute
                        : BrandColors.textMuted,
                  ),
                ],
              ),
              if (route.vehiculoPlaca.isNotEmpty ||
                  route.fechaDespacho != null) ...[
                const SizedBox(height: 14),
                Wrap(
                  spacing: 16,
                  runSpacing: 6,
                  children: [
                    if (route.vehiculoPlaca.isNotEmpty)
                      _Meta(
                        icon: Icons.local_shipping_outlined,
                        text: route.vehiculoPlaca,
                      ),
                    if (route.fechaDespacho != null)
                      _Meta(
                        icon: Icons.event_outlined,
                        text: DateFormat(
                          'dd/MM · HH:mm',
                        ).format(route.fechaDespacho!),
                      ),
                  ],
                ),
              ],
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      '${route.entregados} de ${route.totalPedidos} entregadas',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  Text(
                    '$porcentaje %',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: BrandColors.delivered,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              ClipRRect(
                borderRadius: BorderRadius.circular(99),
                child: LinearProgressIndicator(
                  value: route.avance,
                  minHeight: 8,
                  backgroundColor: BrandColors.surfaceAlt,
                  color: BrandColors.delivered,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                route.pendientes == 0
                    ? 'Sin paradas pendientes'
                    : '${route.pendientes} ${route.pendientes == 1 ? "parada pendiente" : "paradas pendientes"}',
                style: theme.textTheme.bodySmall,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Meta extends StatelessWidget {
  const _Meta({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 17, color: BrandColors.textMuted),
        const SizedBox(width: 6),
        Text(text, style: Theme.of(context).textTheme.bodyMedium),
      ],
    );
  }
}
