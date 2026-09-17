import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../data/warehouse_api.dart';
import 'stock_page.dart';

/// Entrada, salida o ajuste de inventario.
///
/// Se guarda en el teléfono y sale por la cola: en la bodega la señal falla,
/// y el conteo no puede esperar a que vuelva. La pantalla muestra antes de
/// guardar cómo queda el stock, que es lo que evita el error de dedo.
class MovementPage extends ConsumerStatefulWidget {
  const MovementPage({super.key, this.product});

  final Product? product;

  @override
  ConsumerState<MovementPage> createState() => _MovementPageState();
}

class _MovementPageState extends ConsumerState<MovementPage> {
  static const _quickReasons = {
    'Entrada': ['Devolución de cliente', 'Reingreso de mercadería', 'Corrección de conteo'],
    'Salida': ['Producto dañado', 'Producto vencido', 'Consumo interno', 'Muestra comercial'],
    'Ajuste': ['Conteo físico', 'Inventario mensual', 'Corrección de sistema'],
  };

  late Product? _product = widget.product;
  String _type = 'Entrada';
  final _quantity = TextEditingController(text: '1');
  final _reason = TextEditingController();
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    if (_type == 'Ajuste' && _product != null) _quantity.text = '${_product!.stock}';
  }

  @override
  void dispose() {
    _quantity.dispose();
    _reason.dispose();
    super.dispose();
  }

  int get _qty => int.tryParse(_quantity.text) ?? 0;

  int? get _resultingStock {
    final product = _product;
    if (product == null) return null;
    return switch (_type) {
      'Entrada' => product.stock + _qty,
      'Salida' => product.stock - _qty,
      _ => _qty,
    };
  }

  String? get _problem {
    if (_product == null) return 'Elegí el producto.';
    if (_type != 'Ajuste' && _qty <= 0) return 'La cantidad debe ser mayor a cero.';
    if ((_resultingStock ?? 0) < 0) return 'No hay suficiente stock para esa salida.';
    if (_type != 'Entrada' && _reason.text.trim().isEmpty) return 'Indicá el motivo.';
    if (_type == 'Ajuste' && _qty == _product!.stock) return 'El ajuste no cambia el stock actual.';
    return null;
  }

  Future<void> _pickProduct() async {
    final picked = await showModalBottomSheet<Product>(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _ProductPicker(),
    );
    if (picked == null) return;
    setState(() {
      _product = picked;
      if (_type == 'Ajuste') _quantity.text = '${picked.stock}';
    });
  }

  Future<void> _save() async {
    final product = _product;
    if (_problem != null || product == null) return;

    final confirmed = await confirmSheet(
      context,
      title: 'Confirmar movimiento',
      message: '${_type == 'Ajuste' ? 'Ajuste a $_qty' : '$_type de $_qty'} unidades de ${product.name}. '
          'El stock pasa de ${product.stock} a $_resultingStock.',
      confirmLabel: 'Registrar',
      icon: Icons.swap_vert_rounded,
    );
    if (!confirmed || !mounted) return;

    setState(() => _saving = true);
    await ref.read(warehouseApiProvider).registerMovement(
          product: product,
          type: _type,
          quantity: _qty,
          reason: _reason.text.trim(),
        );
    ref.invalidate(productsProvider);
    if (!mounted) return;

    showSuccess(context, 'Movimiento registrado. Se envía en cuanto haya señal.');
    Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final product = _product;
    final resulting = _resultingStock;

    return Scaffold(
      appBar: AppBar(title: const Text('Movimiento de inventario')),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
              children: [
                const SectionTitle('Producto'),
                TapCard(
                  onTap: _pickProduct,
                  child: Row(
                    children: [
                      IconBadge(icon: Icons.inventory_2_outlined, color: product == null ? BrandColors.textMuted : BrandColors.redPrimary),
                      const SizedBox(width: 12),
                      Expanded(
                        child: product == null
                            ? Text('Tocá para elegir el producto', style: Theme.of(context).textTheme.bodyLarge?.copyWith(color: BrandColors.textMuted))
                            : Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(product.name, style: Theme.of(context).textTheme.titleMedium),
                                  Text('Stock actual: ${Fmt.number(product.stock)}', style: Theme.of(context).textTheme.bodySmall),
                                ],
                              ),
                      ),
                      const Icon(Icons.unfold_more_rounded, color: BrandColors.textMuted),
                    ],
                  ),
                ),
                const SizedBox(height: 22),
                const SectionTitle('Tipo'),
                SegmentedButton<String>(
                  segments: const [
                    ButtonSegment(value: 'Entrada', label: Text('Entrada'), icon: Icon(Icons.south_west_rounded)),
                    ButtonSegment(value: 'Salida', label: Text('Salida'), icon: Icon(Icons.north_east_rounded)),
                    ButtonSegment(value: 'Ajuste', label: Text('Ajuste'), icon: Icon(Icons.tune_rounded)),
                  ],
                  selected: {_type},
                  showSelectedIcon: false,
                  style: SegmentedButton.styleFrom(
                    minimumSize: const Size.fromHeight(AppTheme.minTouchTarget),
                    selectedBackgroundColor: BrandColors.redPrimary,
                    selectedForegroundColor: BrandColors.white,
                    side: const BorderSide(color: BrandColors.border),
                  ),
                  onSelectionChanged: (value) => setState(() {
                    _type = value.first;
                    if (_type == 'Ajuste' && product != null) _quantity.text = '${product.stock}';
                    if (_type != 'Ajuste' && _qty == (product?.stock ?? -1)) _quantity.text = '1';
                  }),
                ),
                const SizedBox(height: 8),
                Text(
                  switch (_type) {
                    'Entrada' => 'Suma unidades al stock.',
                    'Salida' => 'Resta unidades del stock.',
                    _ => 'Fija el stock en la cantidad contada.',
                  },
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                const SizedBox(height: 22),
                SectionTitle(_type == 'Ajuste' ? 'Cantidad contada' : 'Cantidad'),
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: _quantity,
                        keyboardType: TextInputType.number,
                        inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(6)],
                        style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
                        decoration: const InputDecoration(suffixText: 'unidades'),
                        onChanged: (_) => setState(() {}),
                      ),
                    ),
                    const SizedBox(width: 12),
                    QuantityStepper(
                      value: _qty,
                      onChanged: (value) => setState(() => _quantity.text = '$value'),
                      min: _type == 'Ajuste' ? 0 : 1,
                    ),
                  ],
                ),
                if (product != null && resulting != null) ...[
                  const SizedBox(height: 14),
                  Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: (resulting < 0 ? BrandColors.danger : BrandColors.inRoute).withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
                    ),
                    child: Row(
                      children: [
                        Text('Stock', style: Theme.of(context).textTheme.bodyMedium),
                        const Spacer(),
                        Text('${product.stock}', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
                        const Padding(
                          padding: EdgeInsets.symmetric(horizontal: 8),
                          child: Icon(Icons.arrow_forward_rounded, size: 20, color: BrandColors.textMuted),
                        ),
                        Text(
                          '$resulting',
                          style: TextStyle(
                            fontSize: 22,
                            fontWeight: FontWeight.w800,
                            color: resulting < 0 ? BrandColors.danger : BrandColors.inRoute,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                const SizedBox(height: 22),
                SectionTitle(_type == 'Entrada' ? 'Motivo (opcional)' : 'Motivo'),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final reason in _quickReasons[_type]!)
                      ActionChip(
                        label: Text(reason),
                        onPressed: () => setState(() => _reason.text = reason),
                        backgroundColor: BrandColors.white,
                        side: const BorderSide(color: BrandColors.border),
                      ),
                  ],
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _reason,
                  maxLength: 250,
                  maxLines: 2,
                  onChanged: (_) => setState(() {}),
                  decoration: const InputDecoration(hintText: 'Detalle del movimiento'),
                ),
                const SizedBox(height: 8),
                if (_problem != null && product != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: InlineNotice(message: _problem!, color: BrandColors.pending, icon: Icons.info_outline_rounded),
                  ),
                BusyButton(
                  label: 'Registrar movimiento',
                  icon: Icons.check_rounded,
                  busy: _saving,
                  color: BrandColors.action,
                  onPressed: _problem == null ? _save : null,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Buscador de productos activos para elegir el del movimiento.
class _ProductPicker extends ConsumerStatefulWidget {
  const _ProductPicker();

  @override
  ConsumerState<_ProductPicker> createState() => _ProductPickerState();
}

class _ProductPickerState extends ConsumerState<_ProductPicker> {
  String _search = '';

  @override
  Widget build(BuildContext context) {
    final products = ref.watch(productsProvider((search: _search, filter: 'Todos')));

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.85,
      maxChildSize: 0.95,
      builder: (context, controller) => Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 10),
            child: AppSearchField(hint: 'Buscar producto', onChanged: (value) => setState(() => _search = value)),
          ),
          Expanded(
            child: products.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => ErrorView(message: friendlyError(error)),
              data: (items) {
                final active = items.where((product) => product.active).toList();
                if (active.isEmpty) {
                  return const EmptyState(icon: Icons.search_off_rounded, title: 'Sin resultados', message: 'Probá con otro nombre.');
                }
                return ListView.builder(
                  controller: controller,
                  itemCount: active.length,
                  itemBuilder: (context, index) {
                    final product = active[index];
                    final (color, label) = stockStyle(product);
                    return ListTile(
                      title: Text(product.name),
                      subtitle: Text('${product.category} · $label'),
                      trailing: Text(Fmt.number(product.stock), style: TextStyle(fontWeight: FontWeight.w800, color: color, fontSize: 16)),
                      onTap: () => Navigator.of(context).pop(product),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
