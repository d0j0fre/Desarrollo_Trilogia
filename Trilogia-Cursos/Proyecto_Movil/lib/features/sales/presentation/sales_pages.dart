import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../data/sales_api.dart';

final sellerOrdersProvider = FutureProvider.autoDispose<List<SellerOrder>>(
  (ref) => ref.watch(salesApiProvider).myOrders(),
);

final saleClientsProvider = FutureProvider.autoDispose.family<List<SaleClient>, String>(
  (ref, search) => ref.watch(salesApiProvider).clients(search),
);

final saleProductsProvider = FutureProvider.autoDispose.family<List<SaleProduct>, String>(
  (ref, search) => ref.watch(salesApiProvider).products(search),
);

/// Ventas del vendedor: lo que ya registró y el botón para tomar un pedido.
class SalesPage extends ConsumerWidget {
  const SalesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final orders = ref.watch(sellerOrdersProvider);

    // Una venta que sale de la cola cambia la lista: se vuelve a pedir.
    ref.listen(syncStateProvider, (previous, next) {
      if ((previous?.value?.enviando ?? false) && !(next.value?.enviando ?? true)) {
        ref.invalidate(sellerOrdersProvider);
      }
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Ventas en campo')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/ventas/nueva'),
        backgroundColor: BrandColors.redPrimary,
        foregroundColor: BrandColors.white,
        icon: const Icon(Icons.add_shopping_cart_rounded),
        label: const Text('Nuevo pedido', style: TextStyle(fontWeight: FontWeight.w700)),
      ),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: AsyncListBody<SellerOrder>(
              value: orders,
              onRefresh: () async => ref.invalidate(sellerOrdersProvider),
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
              emptyIcon: Icons.shopping_bag_outlined,
              emptyTitle: 'Todavía no registraste ventas',
              emptyMessage: 'Tocá "Nuevo pedido" para tomar el primero.',
              header: const Padding(padding: EdgeInsets.only(left: 4, bottom: 8), child: SectionTitle('Mis últimas ventas')),
              itemBuilder: (context, order) {
                final color = OrderStatusStyle.color(order.status);
                return Card(
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                    leading: IconBadge(icon: OrderStatusStyle.icon(order.status), color: color),
                    title: Text(order.client, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w600)),
                    subtitle: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('#${order.id} · ${Fmt.shortDateTime(order.date)}'),
                        const SizedBox(height: 4),
                        Wrap(spacing: 6, runSpacing: 4, children: [
                          StatusChip(label: Fmt.status(order.status), color: color),
                          if (order.invoiceNumber.isNotEmpty)
                            StatusChip(label: order.invoiceNumber, color: BrandColors.view, icon: Icons.receipt_rounded),
                        ]),
                      ],
                    ),
                    trailing: Text(Fmt.money(order.total), style: const TextStyle(fontWeight: FontWeight.w800)),
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

/// Toma de pedido en tres pasos: cliente, productos y entrega.
///
/// Los precios y el stock son informativos: el procedimiento del sitio web
/// recalcula el total, valida stock y decide si el pedido queda retenido.
class NewSalePage extends ConsumerStatefulWidget {
  const NewSalePage({super.key});

  @override
  ConsumerState<NewSalePage> createState() => _NewSalePageState();
}

class _NewSalePageState extends ConsumerState<NewSalePage> {
  int _step = 0;
  SaleClient? _client;
  final Map<SaleProduct, int> _cart = {};
  String _deliveryType = SalesApi.deliveryTypes.first;
  final _address = TextEditingController();
  final _notes = TextEditingController();
  String _clientSearch = '';
  String _productSearch = '';
  bool _saving = false;

  @override
  void dispose() {
    _address.dispose();
    _notes.dispose();
    super.dispose();
  }

  double get _total => _cart.entries.fold(0, (sum, e) => sum + e.key.price * e.value);
  int get _units => _cart.values.fold(0, (sum, q) => sum + q);

  void _setQuantity(SaleProduct product, int quantity) => setState(() {
        if (quantity <= 0) {
          _cart.remove(product);
        } else {
          _cart[product] = quantity.clamp(1, product.stock);
        }
      });

  Future<void> _submit() async {
    final client = _client;
    if (client == null || _cart.isEmpty || _address.text.trim().isEmpty) return;

    final online = ref.read(syncStateProvider).value?.hayConexion ?? true;
    final confirmed = await confirmSheet(
      context,
      title: 'Registrar pedido',
      message: '${client.name} · $_units unidades · ${Fmt.money(_total)} antes de impuestos.\n'
          '${online ? 'Se envía ahora.' : 'Estás sin señal: se guarda y se envía al recuperarla.'}',
      confirmLabel: 'Registrar pedido',
      icon: Icons.shopping_cart_checkout_rounded,
    );
    if (!confirmed || !mounted) return;

    setState(() => _saving = true);
    await ref.read(salesApiProvider).submit(
          client: client,
          deliveryType: _deliveryType,
          address: _address.text.trim(),
          notes: _notes.text.trim(),
          items: Map.of(_cart),
          online: online,
        );
    if (!mounted) return;
    showSuccess(context, online ? 'Pedido enviado. El estado aparece en tus ventas.' : 'Pedido guardado. Se envía al recuperar señal.');
    Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final titles = ['Cliente', 'Productos', 'Entrega'];

    return PopScope(
      canPop: _step == 0,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) setState(() => _step -= 1);
      },
      child: Scaffold(
        appBar: AppBar(title: Text('Nuevo pedido · ${titles[_step]}')),
        body: Column(
          children: [
            const SyncBanner(),
            _StepIndicator(step: _step, titles: titles),
            Expanded(
              child: switch (_step) {
                0 => _clientStep(),
                1 => _productStep(),
                _ => _deliveryStep(),
              },
            ),
            _bottomBar(),
          ],
        ),
      ),
    );
  }

  Widget _clientStep() {
    final clients = ref.watch(saleClientsProvider(_clientSearch));
    return Column(
      children: [
        ListToolbar(search: AppSearchField(hint: 'Buscar cliente por nombre, correo o teléfono', onChanged: (v) => setState(() => _clientSearch = v))),
        Expanded(
          child: AsyncListBody<SaleClient>(
            value: clients,
            onRefresh: () async => ref.invalidate(saleClientsProvider(_clientSearch)),
            spacing: 8,
            emptyIcon: Icons.person_search_rounded,
            emptyTitle: 'Sin clientes',
            emptyMessage: 'Ningún cliente activo coincide con la búsqueda.',
            itemBuilder: (context, client) {
              final selected = _client?.id == client.id;
              return Card(
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(AppTheme.radius),
                  side: BorderSide(color: selected ? BrandColors.redPrimary : BrandColors.border, width: selected ? 2 : 1),
                ),
                child: ListTile(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                  leading: CircleAvatar(
                    backgroundColor: BrandColors.redPrimary.withValues(alpha: 0.1),
                    child: Text(client.name.isEmpty ? '?' : client.name[0].toUpperCase(),
                        style: const TextStyle(color: BrandColors.redPrimary, fontWeight: FontWeight.w800)),
                  ),
                  title: Text(client.name, style: const TextStyle(fontWeight: FontWeight.w600)),
                  subtitle: Text([client.phone, client.address].where((t) => t.isNotEmpty).join(' · '), maxLines: 2, overflow: TextOverflow.ellipsis),
                  trailing: selected ? const Icon(Icons.check_circle_rounded, color: BrandColors.redPrimary) : null,
                  onTap: () => setState(() {
                    _client = client;
                    if (_address.text.trim().isEmpty) _address.text = client.address;
                    _step = 1;
                  }),
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _productStep() {
    final products = ref.watch(saleProductsProvider(_productSearch));
    return Column(
      children: [
        ListToolbar(search: AppSearchField(hint: 'Buscar producto o categoría', onChanged: (v) => setState(() => _productSearch = v))),
        Expanded(
          child: AsyncListBody<SaleProduct>(
            value: products,
            onRefresh: () async => ref.invalidate(saleProductsProvider(_productSearch)),
            spacing: 8,
            emptyIcon: Icons.search_off_rounded,
            emptyTitle: 'Sin productos',
            emptyMessage: 'Solo se muestran productos activos con stock.',
            itemBuilder: (context, product) {
              final entry = _cart.entries.where((e) => e.key.id == product.id).firstOrNull;
              final quantity = entry?.value ?? 0;
              final key = entry?.key ?? product;
              return Card(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 8, 12),
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(product.name, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16)),
                            Text('${Fmt.money(product.price)} · ${product.stock} disponibles', style: Theme.of(context).textTheme.bodySmall),
                          ],
                        ),
                      ),
                      quantity == 0
                          ? IconButton.filled(
                              onPressed: () => _setQuantity(key, 1),
                              tooltip: 'Agregar',
                              style: IconButton.styleFrom(backgroundColor: BrandColors.redPrimary),
                              icon: const Icon(Icons.add_rounded),
                            )
                          : QuantityStepper(value: quantity, max: product.stock, onChanged: (q) => _setQuantity(key, q)),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _deliveryStep() {
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
      children: [
        const SectionTitle('Resumen'),
        Card(
          child: Column(
            children: [
              ListTile(
                leading: const Icon(Icons.person_outline_rounded),
                title: Text(_client?.name ?? ''),
                trailing: TextButton(onPressed: () => setState(() => _step = 0), child: const Text('Cambiar')),
              ),
              const Divider(indent: 16, endIndent: 16),
              for (final entry in _cart.entries)
                ListTile(
                  dense: true,
                  title: Text(entry.key.name),
                  subtitle: Text('${entry.value} × ${Fmt.money(entry.key.price)}'),
                  trailing: Text(Fmt.money(entry.key.price * entry.value), style: const TextStyle(fontWeight: FontWeight.w700)),
                ),
              const Divider(indent: 16, endIndent: 16),
              ListTile(
                title: const Text('Subtotal', style: TextStyle(fontWeight: FontWeight.w800)),
                subtitle: const Text('El impuesto se calcula al facturar'),
                trailing: Text(Fmt.money(_total), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
              ),
            ],
          ),
        ),
        const SizedBox(height: 20),
        const SectionTitle('Entrega'),
        for (final type in SalesApi.deliveryTypes)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Card(
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
                side: BorderSide(color: _deliveryType == type ? BrandColors.redPrimary : BrandColors.border, width: _deliveryType == type ? 2 : 1),
              ),
              child: ListTile(
                leading: Icon(switch (type) {
                  'Retiro en local' => Icons.storefront_outlined,
                  'Envío a domicilio' => Icons.local_shipping_outlined,
                  _ => Icons.directions_car_outlined,
                }, color: _deliveryType == type ? BrandColors.redPrimary : BrandColors.textMuted),
                title: Text(type),
                trailing: _deliveryType == type ? const Icon(Icons.check_circle_rounded, color: BrandColors.redPrimary) : null,
                onTap: () => setState(() => _deliveryType = type),
              ),
            ),
          ),
        const SizedBox(height: 8),
        TextField(
          controller: _address,
          maxLength: 250,
          maxLines: 2,
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(labelText: 'Dirección de entrega', prefixIcon: Icon(Icons.location_on_outlined)),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: _notes,
          maxLength: 250,
          maxLines: 2,
          decoration: const InputDecoration(labelText: 'Observaciones (opcional)', prefixIcon: Icon(Icons.notes_rounded)),
        ),
      ],
    );
  }

  Widget _bottomBar() {
    final canContinue = switch (_step) {
      0 => _client != null,
      1 => _cart.isNotEmpty,
      _ => _address.text.trim().isNotEmpty && !_saving,
    };

    return Material(
      color: BrandColors.white,
      elevation: 8,
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 10),
          child: Row(
            children: [
              if (_cart.isNotEmpty)
                Expanded(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('$_units unidades', style: Theme.of(context).textTheme.bodySmall),
                      Text(Fmt.money(_total), style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
                    ],
                  ),
                )
              else
                const Spacer(),
              SizedBox(
                width: 190,
                child: _step < 2
                    ? ElevatedButton(
                        onPressed: canContinue ? () => setState(() => _step += 1) : null,
                        child: const Text('Continuar'),
                      )
                    : BusyButton(label: 'Registrar', icon: Icons.check_rounded, busy: _saving, color: BrandColors.action,
                        onPressed: canContinue ? _submit : null),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _StepIndicator extends StatelessWidget {
  const _StepIndicator({required this.step, required this.titles});

  final int step;
  final List<String> titles;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 2),
      child: Row(
        children: [
          for (var i = 0; i < titles.length; i++)
            Expanded(
              child: Padding(
                padding: EdgeInsets.only(right: i < titles.length - 1 ? 6 : 0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      height: 5,
                      decoration: BoxDecoration(
                        color: i <= step ? BrandColors.redPrimary : BrandColors.surfaceAlt,
                        borderRadius: BorderRadius.circular(99),
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text('${i + 1}. ${titles[i]}',
                        style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600,
                            color: i <= step ? BrandColors.redPrimary : BrandColors.textMuted)),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

