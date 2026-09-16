import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../shared/utils/navigation_launcher.dart';
import '../application/routes_controller.dart';
import '../domain/delivery.dart';
import '../domain/route.dart';

class RouteDetailPage extends ConsumerWidget {
  const RouteDetailPage({super.key, required this.rutaId});

  final int rutaId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(routeDetailProvider(rutaId));

    return Scaffold(
      appBar: AppBar(
        title: Text(detail.value?.codigo ?? 'Ruta'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Actualizar',
            onPressed: detail.isLoading
                ? null
                : () =>
                      ref.read(routeDetailProvider(rutaId).notifier).refresh(),
          ),
        ],
      ),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: detail.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => ErrorView(
                message:
                    'Esta ruta ya no está disponible para vos. Puede que operaciones la haya reasignado.',
                onRetry: () =>
                    ref.read(routeDetailProvider(rutaId).notifier).refresh(),
              ),
              data: (route) => route == null
                  ? const EmptyState(
                      icon: Icons.route_rounded,
                      title: 'Ruta no encontrada',
                      message: 'Volvé a la lista y actualizá.',
                    )
                  : _RouteBody(route: route, rutaId: rutaId),
            ),
          ),
        ],
      ),
    );
  }
}

class _RouteBody extends ConsumerWidget {
  const _RouteBody({required this.route, required this.rutaId});

  final DriverRoute route;
  final int rutaId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final total = route.entregas.length;
    final cerradas = route.entregas.where((d) => d.estaCerrada).length;
    final pendientes = total - cerradas;
    final avance = total == 0 ? 0.0 : cerradas / total;
    final theme = Theme.of(context);

    return RefreshIndicator(
      onRefresh: () => ref.read(routeDetailProvider(rutaId).notifier).refresh(),
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(18),
              child: Row(
                children: [
                  SizedBox(
                    width: 64,
                    height: 64,
                    child: Stack(
                      fit: StackFit.expand,
                      children: [
                        CircularProgressIndicator(
                          value: avance,
                          strokeWidth: 7,
                          strokeCap: StrokeCap.round,
                          backgroundColor: BrandColors.surfaceAlt,
                          color: BrandColors.delivered,
                        ),
                        Center(
                          child: Text(
                            '$cerradas/$total',
                            style: const TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 18),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          route.zona.isEmpty
                              ? 'Ruta ${route.codigo}'
                              : route.zona,
                          style: theme.textTheme.titleLarge,
                        ),
                        const SizedBox(height: 4),
                        Text(
                          pendientes == 0
                              ? 'Todas las paradas atendidas'
                              : '$pendientes ${pendientes == 1 ? "parada pendiente" : "paradas pendientes"} de $total',
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: BrandColors.textMuted,
                          ),
                        ),
                        if (route.sincronizadoEn != null) ...[
                          const SizedBox(height: 6),
                          Row(
                            children: [
                              const Icon(
                                Icons.update_rounded,
                                size: 15,
                                color: BrandColors.textMuted,
                              ),
                              const SizedBox(width: 4),
                              Text(
                                'Actualizado ${DateFormat('dd/MM HH:mm').format(route.sincronizadoEn!)}',
                                style: theme.textTheme.bodySmall,
                              ),
                            ],
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 22),
          const SectionTitle('Paradas'),
          ...route.entregas.map(
            (delivery) => Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: DeliveryCard(delivery: delivery, rutaId: rutaId),
            ),
          ),
        ],
      ),
    );
  }
}

/// Una parada.
///
/// El orden de los elementos sigue lo que hace el chofer: ver a quien va,
/// llegar, y recien entonces marcar. Por eso navegar y llamar estan arriba, y
/// las acciones que cierran la entrega abajo.
class DeliveryCard extends ConsumerWidget {
  const DeliveryCard({super.key, required this.delivery, required this.rutaId});

  final Delivery delivery;
  final int rutaId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final color = AppTheme.deliveryStatusColor(delivery.estadoEntrega);
    final moneda = NumberFormat.currency(
      locale: 'es_CR',
      symbol: '₡',
      decimalDigits: 0,
      // En Costa Rica el simbolo va antes del monto: ₡184.500.
      customPattern: '¤#,##0',
    );
    final theme = Theme.of(context);
    final puedeNavegar =
        delivery.tieneCoordenadas || delivery.direccion.trim().isNotEmpty;

    return Card(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppTheme.radius),
        side: BorderSide(
          color: delivery.estaCerrada
              ? BrandColors.border
              : BrandColors.redPrimary.withValues(alpha: 0.18),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 36,
                  height: 36,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: delivery.estaCerrada
                        ? color.withValues(alpha: 0.12)
                        : BrandColors.redPrimary,
                    shape: BoxShape.circle,
                  ),
                  child: delivery.estaCerrada
                      ? Icon(
                          AppTheme.deliveryStatusIcon(delivery.estadoEntrega),
                          size: 20,
                          color: color,
                        )
                      : Text(
                          '${delivery.secuencia}',
                          style: const TextStyle(
                            color: BrandColors.white,
                            fontWeight: FontWeight.w800,
                            fontSize: 15,
                          ),
                        ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        delivery.cliente,
                        style: theme.textTheme.titleMedium,
                      ),
                      const SizedBox(height: 2),
                      Text(
                        'Pedido #${delivery.pedidoId} · ${moneda.format(delivery.total)}',
                        style: theme.textTheme.bodySmall,
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    StatusChip(
                      label: AppTheme.deliveryStatusLabel(
                        delivery.estadoEntrega,
                      ),
                      color: color,
                      icon: AppTheme.deliveryStatusIcon(delivery.estadoEntrega),
                    ),
                    if (delivery.pendienteDeSincronizar) ...[
                      const SizedBox(height: 6),
                      const StatusChip(
                        label: 'Por enviar',
                        color: BrandColors.offline,
                        icon: Icons.cloud_upload_outlined,
                      ),
                    ],
                  ],
                ),
              ],
            ),
            const SizedBox(height: 14),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: BrandColors.surface,
                borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(
                    Icons.location_on_outlined,
                    size: 20,
                    color: BrandColors.redPrimary,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      delivery.direccion.isEmpty
                          ? 'Sin dirección registrada'
                          : delivery.direccion,
                      style: theme.textTheme.bodyMedium,
                    ),
                  ),
                ],
              ),
            ),
            if (delivery.motivoFallo.isNotEmpty) ...[
              const SizedBox(height: 10),
              InlineNotice(
                message: 'Motivo: ${delivery.motivoFallo}',
                icon: Icons.info_outline_rounded,
              ),
            ],
            if (puedeNavegar || delivery.telefono.isNotEmpty) ...[
              const SizedBox(height: 12),
              Row(
                children: [
                  if (puedeNavegar)
                    Expanded(
                      child: OutlinedButton.icon(
                        icon: const Icon(Icons.navigation_outlined, size: 20),
                        label: const Text('Navegar'),
                        onPressed: () => _navegar(context),
                      ),
                    ),
                  if (puedeNavegar && delivery.telefono.isNotEmpty)
                    const SizedBox(width: 10),
                  if (delivery.telefono.isNotEmpty)
                    Expanded(
                      child: OutlinedButton.icon(
                        icon: const Icon(Icons.phone_outlined, size: 20),
                        label: const Text('Llamar'),
                        onPressed: () => _llamar(context),
                      ),
                    ),
                ],
              ),
            ],
            if (!delivery.estaCerrada) ...[
              const SizedBox(height: 10),
              Row(
                children: [
                  // Sin icono a proposito: con el, "No entregado" no cabe en
                  // media pantalla y Android parte la palabra en dos lineas.
                  // El color y la posicion ya distinguen las dos acciones.
                  Expanded(
                    child: ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: BrandColors.action,
                      ),
                      onPressed: () => _marcarEntregado(context, ref),
                      child: const Text('Entregado'),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: BrandColors.danger,
                      ),
                      onPressed: () => _marcarFallido(context, ref),
                      child: const Text('No entregado'),
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

  Future<void> _navegar(BuildContext context) async {
    final abierto = delivery.tieneCoordenadas
        ? await NavigationLauncher.abrirRuta(
            latitud: delivery.latitud!,
            longitud: delivery.longitud!,
          )
        : await NavigationLauncher.buscarDireccion(delivery.direccion);

    if (!abierto && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'No se encontró ninguna aplicación de mapas instalada.',
          ),
        ),
      );
    }
  }

  Future<void> _llamar(BuildContext context) async {
    final abierto = await NavigationLauncher.llamar(delivery.telefono);
    if (!abierto && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'No se pudo abrir el marcador. Número: ${delivery.telefono}',
          ),
        ),
      );
    }
  }

  Future<void> _marcarEntregado(BuildContext context, WidgetRef ref) async {
    final confirmado = await confirmSheet(
      context,
      title: 'Confirmar entrega',
      message:
          'Vas a marcar el pedido #${delivery.pedidoId} de ${delivery.cliente} como entregado. '
          'Se le va a avisar al cliente.',
      confirmLabel: 'Sí, fue entregado',
      icon: Icons.check_circle_outline_rounded,
    );

    if (!confirmado) return;

    await ref
        .read(routeDetailProvider(rutaId).notifier)
        .marcar(
          rutaPedidoId: delivery.rutaPedidoId,
          pedidoId: delivery.pedidoId,
          estado: DeliveryStatus.entregado,
        );

    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Pedido #${delivery.pedidoId} marcado como entregado.'),
        ),
      );
    }
  }

  Future<void> _marcarFallido(BuildContext context, WidgetRef ref) async {
    // El motivo es obligatorio y la API lo exige: una entrega fallida sin
    // explicacion no le sirve a nadie en la oficina al dia siguiente.
    final motivo = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (context) => const _MotivoSheet(),
    );

    if (motivo == null || motivo.trim().isEmpty) return;

    await ref
        .read(routeDetailProvider(rutaId).notifier)
        .marcar(
          rutaPedidoId: delivery.rutaPedidoId,
          pedidoId: delivery.pedidoId,
          estado: DeliveryStatus.fallido,
          motivoFallo: motivo.trim(),
        );

    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Pedido #${delivery.pedidoId} marcado como no entregado.',
          ),
        ),
      );
    }
  }
}

class _MotivoSheet extends StatefulWidget {
  const _MotivoSheet();

  @override
  State<_MotivoSheet> createState() => _MotivoSheetState();
}

class _MotivoSheetState extends State<_MotivoSheet> {
  final _controller = TextEditingController();
  String? _seleccionado;

  /// Motivos frecuentes, para que el chofer no tenga que escribir de pie.
  /// «Otro» abre el campo libre.
  static const _motivos = [
    'Cliente ausente',
    'Local cerrado',
    'Dirección incorrecta',
    'Cliente rechazó el pedido',
    'No hubo pago',
    'Otro',
  ];

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final esOtro = _seleccionado == 'Otro';
    final motivoListo =
        _seleccionado != null &&
        (!esOtro || _controller.text.trim().isNotEmpty);

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          left: 24,
          right: 24,
          bottom: MediaQuery.viewInsetsOf(context).bottom + 16,
        ),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                '¿Por qué no se entregó?',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 6),
              Text(
                'Operaciones y el cliente van a ver este motivo.',
                style: Theme.of(
                  context,
                ).textTheme.bodyMedium?.copyWith(color: BrandColors.textMuted),
              ),
              const SizedBox(height: 16),
              // RadioGroup en vez de groupValue/onChanged por radio: la API
              // anterior quedo obsoleta despues de Flutter 3.32.
              RadioGroup<String>(
                groupValue: _seleccionado,
                onChanged: (value) => setState(() => _seleccionado = value),
                child: Column(
                  children: _motivos.map((motivo) {
                    final elegido = _seleccionado == motivo;
                    return Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Material(
                        color: elegido
                            ? BrandColors.danger.withValues(alpha: 0.06)
                            : BrandColors.white,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(
                            AppTheme.radiusSmall,
                          ),
                          side: BorderSide(
                            color: elegido
                                ? BrandColors.danger
                                : BrandColors.border,
                            width: elegido ? 1.5 : 1,
                          ),
                        ),
                        clipBehavior: Clip.antiAlias,
                        child: RadioListTile<String>(
                          value: motivo,
                          title: Text(motivo),
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 8,
                          ),
                        ),
                      ),
                    );
                  }).toList(),
                ),
              ),
              if (esOtro) ...[
                const SizedBox(height: 4),
                TextField(
                  controller: _controller,
                  autofocus: true,
                  maxLength: 300,
                  maxLines: 2,
                  onChanged: (_) => setState(() {}),
                  decoration: const InputDecoration(
                    labelText: 'Escribí el motivo',
                  ),
                ),
              ],
              const SizedBox(height: 12),
              ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: BrandColors.danger,
                ),
                onPressed: !motivoListo
                    ? null
                    : () {
                        final motivo = esOtro
                            ? _controller.text.trim()
                            : _seleccionado!;
                        Navigator.of(context).pop(motivo);
                      },
                child: const Text('Marcar como no entregado'),
              ),
              const SizedBox(height: 4),
              TextButton(
                style: TextButton.styleFrom(
                  foregroundColor: BrandColors.textMuted,
                  minimumSize: const Size.fromHeight(AppTheme.minTouchTarget),
                ),
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('Cancelar'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
