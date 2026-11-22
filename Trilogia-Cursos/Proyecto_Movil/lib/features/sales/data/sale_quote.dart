import '../../../shared/utils/formatters.dart';

/// Una línea de la cotización: lo que el servidor cobraría por ese producto
/// después de aplicar las promociones activas.
class SaleQuoteLine {
  const SaleQuoteLine({
    required this.productId,
    required this.name,
    required this.quantity,
    required this.listPrice,
    required this.discountAmount,
    required this.unitPrice,
    required this.subtotal,
    required this.isGift,
    required this.promotion,
  });

  final int productId;
  final String name;
  final int quantity;

  /// Precio de lista por unidad.
  final double listPrice;

  /// Descuento total de la línea (no por unidad).
  final double discountAmount;

  /// Precio por unidad ya con descuento. Cero en una regalía.
  final double unitPrice;
  final double subtotal;

  /// Regalía de una promoción: va a precio cero y se muestra como «GRATIS».
  final bool isGift;

  /// Nombre de la promoción aplicada; vacío si no hay.
  final String promotion;

  bool get hasDiscount => !isGift && discountAmount > 0;

  factory SaleQuoteLine.fromJson(Map<String, dynamic> json) => SaleQuoteLine(
        productId: J.integer(json['productoId']),
        name: J.text(json['nombre']),
        quantity: J.integer(json['cantidad']),
        listPrice: J.decimal(json['precioLista']),
        discountAmount: J.decimal(json['montoDescuento']),
        unitPrice: J.decimal(json['precioUnitario']),
        subtotal: J.decimal(json['subtotal']),
        isGift: J.boolean(json['esRegalo']),
        promotion: J.text(json['promocion']),
      );
}

/// Cotización del pedido: el precio con promociones, el IVA y el total.
///
/// Viene del servidor (`sales/quote`) o, sin conexión, se arma en el teléfono
/// con [SaleQuote.estimate] a precio de lista (`estimated == true`).
class SaleQuote {
  const SaleQuote({
    required this.lines,
    required this.listSubtotal,
    required this.discount,
    required this.subtotal,
    required this.tax,
    required this.total,
    this.retentionThreshold = 0,
    this.retained = false,
    this.estimated = false,
  });

  final List<SaleQuoteLine> lines;

  /// Suma a precio de lista, antes de descuentos.
  final double listSubtotal;
  final double discount;

  /// Ya después del descuento y antes del IVA.
  final double subtotal;
  final double tax;

  /// Total con IVA.
  final double total;
  final double retentionThreshold;

  /// El total con IVA supera el umbral: el pedido queda para aprobación del Gerente.
  final bool retained;

  /// Calculada en el teléfono, sin promociones.
  final bool estimated;

  factory SaleQuote.fromJson(Map<String, dynamic> json) => SaleQuote(
        lines: J.list(json['lineas'], SaleQuoteLine.fromJson),
        listSubtotal: J.decimal(json['subtotalLista']),
        discount: J.decimal(json['descuento']),
        subtotal: J.decimal(json['subtotal']),
        tax: J.decimal(json['impuesto']),
        total: J.decimal(json['total']),
        retentionThreshold: J.decimal(json['umbralRetencion']),
        retained: J.boolean(json['retenido']),
      );

  /// Estimado local: precio de lista, sin promociones y con el IVA calculado
  /// aquí. [items] son (producto, cantidad) en el orden del carrito.
  factory SaleQuote.estimate(Iterable<({int productId, String name, double price, int quantity})> items) {
    final lines = <SaleQuoteLine>[];
    var cents = 0;
    for (final item in items) {
      final lineCents = SalePricing.toCents(item.price) * item.quantity;
      cents += lineCents;
      lines.add(SaleQuoteLine(
        productId: item.productId,
        name: item.name,
        quantity: item.quantity,
        listPrice: item.price,
        discountAmount: 0,
        unitPrice: item.price,
        subtotal: SalePricing.fromCents(lineCents),
        isGift: false,
        promotion: '',
      ));
    }
    final taxCents = SalePricing.taxCents(cents);
    return SaleQuote(
      lines: lines,
      listSubtotal: SalePricing.fromCents(cents),
      discount: 0,
      subtotal: SalePricing.fromCents(cents),
      tax: SalePricing.fromCents(taxCents),
      total: SalePricing.fromCents(cents + taxCents),
      estimated: true,
    );
  }
}

/// Aritmética del IVA en céntimos enteros, para que el redondeo sea el mismo
/// que el de la factura (2 decimales, mitad hacia arriba) y no dependa de los
/// errores de la coma flotante.
class SalePricing {
  const SalePricing._();

  /// IVA de la factura: 13 %.
  static const taxPercent = 13;

  static int toCents(double value) => (value * 100).round();

  static double fromCents(int cents) => cents / 100;

  static int taxCents(int subtotalCents) => subtotalCents <= 0 ? 0 : (subtotalCents * taxPercent + 50) ~/ 100;

  /// IVA de un subtotal, a 2 decimales.
  static double tax(double subtotal) => fromCents(taxCents(toCents(subtotal)));

  /// Total con IVA de un subtotal, a 2 decimales.
  static double totalWithTax(double subtotal) {
    final cents = toCents(subtotal);
    return fromCents(cents + taxCents(cents));
  }
}
