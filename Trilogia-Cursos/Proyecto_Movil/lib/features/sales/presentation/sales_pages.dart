import 'dart:async';

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
              emptyMessage: 'Toque "Nuevo pedido" para tomar el primero.',
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
/// Los precios y el stock son informativos: el servidor recalcula el total,
/// valida stock y decide si el pedido queda retenido. Con señal, la pantalla
/// pide una cotización (promociones + IVA) para mostrar lo mismo que se va a
/// cobrar; sin señal muestra un estimado a precio de lista.
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
  final _identification = TextEditingController();
  String _clientSearch = '';
  String _productSearch = '';
  bool _saving = false;

  // Cotización del servidor. Se pide con un pequeño retraso para no disparar
  // una petición por cada toque del "+", y una respuesta vieja se descarta.
  static const _quoteDelay = Duration(milliseconds: 400);
  Timer? _quoteTimer;
  int _quoteRequest = 0;
  SaleQuote? _quote;
  String? _quoteSignature;
  String? _failedSignature;

  @override
  void dispose() {
    _quoteTimer?.cancel();
    _address.dispose();
    _notes.dispose();
    _identification.dispose();
    super.dispose();
  }

  bool get _online => ref.read(syncStateProvider).value?.hayConexion ?? true;

  /// Qué se cotizó: cliente + producto y cantidad. Una cotización solo vale
  /// para la firma con la que se pidió.
  String get _signature {
    final lines = _cart.entries.map((e) => '${e.key.id}:${e.value}').toList()..sort();
    return '${_client?.id ?? 0}|${lines.join(',')}';
  }

  /// Lo que se muestra: la cotización si corresponde al carrito actual; si no,
  /// el estimado local a precio de lista.
  SaleQuote get _view {
    final quote = _quote;
    if (quote != null && _quoteSignature == _signature) return quote;
    return SalesApi.estimate(_cart);
  }

  /// Con señal y todavía sin respuesta para el carrito actual.
  bool get _quotePending =>
      _cart.isNotEmpty && _online && _quoteSignature != _signature && _failedSignature != _signature;

  int get _units => _cart.values.fold(0, (sum, q) => sum + q);

  void _setQuantity(SaleProduct product, int quantity) {
    setState(() {
      if (quantity <= 0) {
        _cart.remove(product);
      } else {
        _cart[product] = quantity.clamp(1, product.stock);
      }
    });
    _scheduleQuote();
  }

  void _scheduleQuote() {
    _quoteTimer?.cancel();
    if (_cart.isEmpty) {
      _quoteRequest++; // lo que venga en camino ya no sirve
      return;
    }
    _quoteTimer = Timer(_quoteDelay, _requestQuote);
  }

  Future<void> _requestQuote() async {
    if (!mounted || _cart.isEmpty || !_online) return;
    final signature = _signature;
    final request = ++_quoteRequest;
    try {
      final quote = await ref.read(salesApiProvider).quote(clientId: _client?.id, items: Map.of(_cart));
      if (!mounted || request != _quoteRequest) return;
      setState(() {
        _quote = quote;
        _quoteSignature = signature;
        _failedSignature = null;
      });
    } catch (_) {
      // Sin cotización el vendedor sigue con el estimado: el servidor aplica
      // las promociones igual al registrar el pedido.
      if (!mounted || request != _quoteRequest) return;
      setState(() => _failedSignature = signature);
    }
  }

  Future<void> _submit() async {
    final client = _client;
    if (client == null || _cart.isEmpty || _address.text.trim().isEmpty) return;

    final online = _online;
    final view = _view;
    final confirmed = await confirmSheet(
      context,
      title: 'Registrar pedido',
      message: '${client.name} · $_units unidades · ${Fmt.money(view.total)} con IVA'
          '${view.estimated ? ' (estimado)' : ''}.\n'
          '${view.retained ? 'Este pedido quedará retenido para revisión del Gerente.\n' : ''}'
          '${online ? 'Se envía ahora.' : 'Está sin señal: se guarda y se envía al recuperarla.'}',
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
          identification: _identification.text.trim(),
          items: Map.of(_cart),
          online: online,
          total: view.total,
        );
    if (!mounted) return;
    showSuccess(context, online ? 'Pedido enviado. El estado aparece en sus ventas.' : 'Pedido guardado. Se envía al recuperar señal.');
    Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final titles = ['Cliente', 'Productos', 'Entrega'];

    // Al recuperar la señal se pide la cotización que no se pudo pedir.
    ref.listen(syncStateProvider, (previous, next) {
      final was = previous?.value?.hayConexion ?? true;
      final now = next.value?.hayConexion ?? true;
      if (!was && now) _scheduleQuote();
    });

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
                  onTap: () {
                    setState(() {
                      _client = client;
                      if (_address.text.trim().isEmpty) _address.text = client.address;
                      _step = 1;
                    });
                    _scheduleQuote();
                  },
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
                            if (quantity > 0 && (_quotedLine(product.id)?.hasDiscount ?? false)) ...[
                              const SizedBox(height: 4),
                              _DiscountedPrice(line: _quotedLine(product.id)!),
                            ],
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

  /// Línea cotizada (no regalía) de un producto, solo si la cotización vale
  /// para el carrito actual.
  SaleQuoteLine? _quotedLine(int productId) {
    final quote = _quote;
    if (quote == null || _quoteSignature != _signature) return null;
    return quote.lines.where((l) => l.productId == productId && !l.isGift).firstOrNull;
  }

  Widget _amountRow(String label, String value, {bool strong = false, Color? color}) {
    final style = TextStyle(fontWeight: strong ? FontWeight.w800 : FontWeight.w500, fontSize: strong ? 18 : 15, color: color);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Row(
        children: [
          Expanded(child: Text(label, style: style)),
          Text(value, style: style),
        ],
      ),
    );
  }

  /// Subtotal, descuento, IVA y total, con los avisos de estimado o retención.
  Widget _totals(SaleQuote view) {
    final pending = _quotePending;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (view.discount > 0) ...[
          _amountRow('Subtotal sin descuento', Fmt.money(view.listSubtotal)),
          _amountRow('Descuento', '-${Fmt.money(view.discount)}', color: BrandColors.action),
        ],
        _amountRow('Subtotal', Fmt.money(view.subtotal)),
        _amountRow('IVA (${SalePricing.taxPercent} %)', Fmt.money(view.tax)),
        const Divider(indent: 16, endIndent: 16),
        _amountRow('Total', Fmt.money(view.total), strong: true),
        if (pending)
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 4, 16, 8),
            child: Row(children: [
              SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 2)),
              SizedBox(width: 8),
              Text('Calculando promociones…'),
            ]),
          )
        else if (view.estimated)
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 4, 16, 8),
            child: Text('Estimado: las promociones se aplican al sincronizar'),
          ),
        if (view.retained)
          const Padding(
            padding: EdgeInsets.fromLTRB(12, 4, 12, 12),
            child: InlineNotice(
              color: BrandColors.pending,
              icon: Icons.hourglass_top_rounded,
              message: 'Este pedido quedará retenido para revisión del Gerente.',
            ),
          ),
      ],
    );
  }

  Widget _deliveryStep() {
    final view = _view;
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
              for (final line in view.lines) _QuoteLineTile(line: line),
              const Divider(indent: 16, endIndent: 16),
              _totals(view),
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
          controller: _identification,
          maxLength: 20,
          keyboardType: TextInputType.number,
          decoration: const InputDecoration(
            labelText: 'Identificación del cliente',
            helperText: 'Obligatoria para facturar el pedido (cédula física, jurídica, DIMEX o NITE).',
            prefixIcon: Icon(Icons.badge_outlined),
          ),
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
    final view = _view;
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
                      Text('$_units unidades · total con IVA${view.estimated ? ' (estimado)' : ''}',
                          style: Theme.of(context).textTheme.bodySmall),
                      Text(Fmt.money(view.total), style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
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

/// Etiqueta con el nombre de la promoción aplicada.
class _PromoChip extends StatelessWidget {
  const _PromoChip(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: BrandColors.action.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.local_offer_outlined, size: 13, color: BrandColors.action),
          const SizedBox(width: 4),
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(color: BrandColors.action, fontSize: 12, fontWeight: FontWeight.w700),
            ),
          ),
        ],
      ),
    );
  }
}

/// Precio de lista tachado junto al precio con descuento.
class _DiscountedPrice extends StatelessWidget {
  const _DiscountedPrice({required this.line});

  final SaleQuoteLine line;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 6,
      runSpacing: 4,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Text(
          Fmt.money(line.listPrice),
          style: const TextStyle(decoration: TextDecoration.lineThrough, color: BrandColors.textMuted, fontSize: 13),
        ),
        Text(Fmt.money(line.unitPrice),
            style: const TextStyle(color: BrandColors.action, fontWeight: FontWeight.w800, fontSize: 14)),
        if (line.promotion.isNotEmpty) _PromoChip(line.promotion),
      ],
    );
  }
}

/// Una línea del resumen: cantidad, precio (con descuento si lo hay) y subtotal.
/// Las regalías van como «GRATIS».
class _QuoteLineTile extends StatelessWidget {
  const _QuoteLineTile({required this.line});

  final SaleQuoteLine line;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      dense: true,
      title: Text(line.name),
      subtitle: line.isGift
          ? Wrap(
              spacing: 6,
              runSpacing: 4,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                Text('${line.quantity} ×'),
                const Text('GRATIS', style: TextStyle(color: BrandColors.action, fontWeight: FontWeight.w800)),
                if (line.promotion.isNotEmpty) _PromoChip(line.promotion),
              ],
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (line.hasDiscount)
                  Wrap(
                    spacing: 6,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text('${line.quantity} ×'),
                      _DiscountedPrice(line: line),
                    ],
                  )
                else ...[
                  Text('${line.quantity} × ${Fmt.money(line.unitPrice)}'),
                  if (line.promotion.isNotEmpty) ...[const SizedBox(height: 4), _PromoChip(line.promotion)],
                ],
              ],
            ),
      trailing: line.isGift
          ? const Text('GRATIS', style: TextStyle(color: BrandColors.action, fontWeight: FontWeight.w800))
          : Text(Fmt.money(line.subtotal), style: const TextStyle(fontWeight: FontWeight.w700)),
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

