import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../../auth/application/auth_controller.dart';
import '../data/warehouse_api.dart';

typedef _ProductQuery = ({String search, String filter});

final productsProvider = FutureProvider.autoDispose.family<List<Product>, _ProductQuery>(
  (ref, query) => ref.watch(warehouseApiProvider).products(search: query.search, filter: query.filter),
);

final productMovementsProvider = FutureProvider.autoDispose.family<List<InventoryMovement>, int>(
  (ref, productId) => ref.watch(warehouseApiProvider).movements(productId),
);

/// Consulta de inventario.
///
/// La misma pantalla sirve a bodega (existencias y movimientos) y a
/// administración (activar o inactivar productos): lo que cambia son las
/// acciones que ofrece la ficha, según los permisos de quien la abre.
class StockPage extends ConsumerStatefulWidget {
  const StockPage({super.key, this.manageMode = false});

  /// Abierta desde "Productos" de gestión: arranca mostrando todo, incluso lo
  /// inactivo, y pone el foco en el estado del catálogo.
  final bool manageMode;

  @override
  ConsumerState<StockPage> createState() => _StockPageState();
}

class _StockPageState extends ConsumerState<StockPage> {
  static const _filters = ['Todos', 'Bajo', 'Agotado', 'Inactivos'];

  String _search = '';
  String _filter = 'Todos';

  _ProductQuery get _query => (search: _search, filter: _filter);

  @override
  Widget build(BuildContext context) {
    final products = ref.watch(productsProvider(_query));

    return Scaffold(
      appBar: AppBar(title: Text(widget.manageMode ? 'Productos' : 'Inventario')),
      body: Column(
        children: [
          const SyncBanner(),
          ListToolbar(
            search: AppSearchField(
              hint: 'Buscar por nombre, categoría o código',
              onChanged: (value) => setState(() => _search = value),
            ),
            filters: FilterChipsBar<String>(
              options: _filters,
              selected: _filter,
              label: (option) => switch (option) {
                'Bajo' => 'Stock bajo',
                'Agotado' => 'Agotados',
                _ => option,
              },
              onSelected: (option) => setState(() => _filter = option),
            ),
          ),
          Expanded(
            child: AsyncListBody<Product>(
              value: products,
              onRefresh: () async => ref.invalidate(productsProvider(_query)),
              emptyIcon: Icons.inventory_2_outlined,
              emptyTitle: 'Sin productos',
              emptyMessage: _search.isEmpty
                  ? 'No hay productos con este filtro.'
                  : 'Ningún producto coincide con "$_search".',
              itemBuilder: (context, product) => _ProductCard(
                product: product,
                onTap: () => _openProduct(product),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _openProduct(Product product) => showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        builder: (_) => ProductSheet(
          product: product,
          onChanged: () => ref.invalidate(productsProvider(_query)),
        ),
      );
}

class _ProductCard extends StatelessWidget {
  const _ProductCard({required this.product, required this.onTap});

  final Product product;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final (color, label) = stockStyle(product);

    return TapCard(
      onTap: onTap,
      child: Row(
        children: [
          Container(
            width: 56,
            height: 56,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(14),
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                FittedBox(
                  child: Text(
                    Fmt.number(product.stock),
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: color),
                  ),
                ),
                Text('unid.', style: TextStyle(fontSize: 11, color: color, fontWeight: FontWeight.w600)),
              ],
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(product.name, style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 2),
                Text('${product.category} · ${Fmt.money(product.price)}', style: Theme.of(context).textTheme.bodySmall),
                const SizedBox(height: 6),
                StatusChip(label: label, color: color),
              ],
            ),
          ),
          const Icon(Icons.chevron_right_rounded, color: BrandColors.textMuted),
        ],
      ),
    );
  }
}

/// Color y etiqueta del estado de existencias. Inactivo pesa más que el stock:
/// un producto fuera del catálogo no se vende aunque tenga unidades.
(Color, String) stockStyle(Product product) {
  if (!product.active) return (BrandColors.textMuted, 'Inactivo');
  return switch (product.stockStatus) {
    'Agotado' => (BrandColors.danger, 'Agotado'),
    'Bajo' => (BrandColors.pending, 'Stock bajo'),
    _ => (BrandColors.delivered, 'Disponible'),
  };
}

/// Ficha del producto: datos, últimos movimientos y acciones permitidas.
class ProductSheet extends ConsumerStatefulWidget {
  const ProductSheet({super.key, required this.product, this.onChanged});

  final Product product;

  /// Avisa a la lista que el producto cambió, se cierre la hoja como se cierre.
  final VoidCallback? onChanged;

  @override
  ConsumerState<ProductSheet> createState() => _ProductSheetState();
}

class _ProductSheetState extends ConsumerState<ProductSheet> {
  late Product _product = widget.product;
  bool _saving = false;

  Future<void> _toggleActive() async {
    final activating = !_product.active;
    final confirmed = await confirmSheet(
      context,
      title: activating ? 'Reactivar producto' : 'Inactivar producto',
      message: activating
          ? '"${_product.name}" vuelve a mostrarse en el catálogo y se puede vender.'
          : '"${_product.name}" deja de mostrarse en la tienda y en la venta en campo. '
              'Las existencias no cambian.',
      confirmLabel: activating ? 'Reactivar' : 'Inactivar',
      confirmColor: activating ? BrandColors.action : BrandColors.danger,
      icon: activating ? Icons.visibility_rounded : Icons.visibility_off_rounded,
    );
    if (!confirmed || !mounted) return;

    setState(() => _saving = true);
    try {
      final updated = await ref.read(warehouseApiProvider).setActive(_product, activating);
      if (!mounted) return;
      setState(() => _product = updated);
      widget.onChanged?.call();
      showSuccess(context, activating ? 'Producto reactivado.' : 'Producto inactivado.');
    } catch (error) {
      if (mounted) showError(context, error);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(sessionProvider);
    final canMove = session?.puede('INVENTARIO_MOVIMIENTOS') ?? false;
    final canEdit = session?.puede('INVENTARIO_EDITAR') ?? false;
    final movements = ref.watch(productMovementsProvider(_product.id));
    final (color, label) = stockStyle(_product);

    return DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.75,
        maxChildSize: 0.95,
        builder: (context, controller) => ListView(
          controller: controller,
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
          children: [
            Text(_product.name, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 4),
            Text('${_product.category} · Código ${_product.id}', style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(child: MetricTile(label: 'Existencias', value: Fmt.number(_product.stock), icon: Icons.inventory_2_outlined, color: color)),
                const SizedBox(width: 10),
                Expanded(child: MetricTile(label: 'Stock mínimo', value: Fmt.number(_product.minStock), icon: Icons.flag_outlined, color: BrandColors.inRoute)),
              ],
            ),
            const SizedBox(height: 10),
            InfoRow(icon: Icons.sell_outlined, label: 'Precio', value: Fmt.money(_product.price)),
            InfoRow(icon: Icons.info_outline_rounded, label: 'Estado', value: label),
            const SizedBox(height: 16),
            if (canMove && _product.active)
              ElevatedButton.icon(
                style: ElevatedButton.styleFrom(backgroundColor: BrandColors.action),
                onPressed: () {
                  Navigator.of(context).pop();
                  context.push('/bodega/movimientos', extra: _product);
                },
                icon: const Icon(Icons.swap_vert_rounded),
                label: const Text('Registrar movimiento'),
              ),
            if (canEdit) ...[
              const SizedBox(height: 10),
              OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  foregroundColor: _product.active ? BrandColors.danger : BrandColors.action,
                  minimumSize: const Size.fromHeight(52),
                ),
                onPressed: _saving ? null : _toggleActive,
                icon: Icon(_product.active ? Icons.visibility_off_rounded : Icons.visibility_rounded),
                label: Text(_saving
                    ? 'Guardando…'
                    : _product.active
                        ? 'Inactivar producto'
                        : 'Reactivar producto'),
              ),
            ],
            const SizedBox(height: 24),
            const SectionTitle('Últimos movimientos'),
            movements.when(
              loading: () => const Padding(padding: EdgeInsets.all(24), child: Center(child: CircularProgressIndicator())),
              error: (error, _) => InlineNotice(message: friendlyError(error), color: BrandColors.offline),
              data: (items) => items.isEmpty
                  ? Text('Sin movimientos registrados.', style: Theme.of(context).textTheme.bodySmall)
                  : Column(children: [for (final movement in items) _MovementRow(movement: movement)]),
            ),
          ],
        ),
    );
  }
}

class _MovementRow extends StatelessWidget {
  const _MovementRow({required this.movement});

  final InventoryMovement movement;

  @override
  Widget build(BuildContext context) {
    final up = movement.after >= movement.before;
    final color = up ? BrandColors.delivered : BrandColors.danger;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          IconBadge(icon: up ? Icons.south_west_rounded : Icons.north_east_rounded, color: color, size: 36),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('${movement.type} · ${movement.before} → ${movement.after}',
                    style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15)),
                if (movement.reason.isNotEmpty)
                  Text(movement.reason, maxLines: 2, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall),
                Text('${movement.user} · ${Fmt.shortDateTime(movement.date)}', style: Theme.of(context).textTheme.bodySmall),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
