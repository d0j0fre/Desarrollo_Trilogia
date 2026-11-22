import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:proyecto_movil/core/network/api_client.dart';
import 'package:proyecto_movil/core/network/api_exception.dart';
import 'package:proyecto_movil/core/sync/sync_service.dart';
import 'package:proyecto_movil/features/management/data/management_api.dart';
import 'package:proyecto_movil/features/sales/data/sales_api.dart';

class _MockApi extends Mock implements ApiClient {}

class _MockSync extends Mock implements SyncService {}

/// Promociones e IVA en la venta móvil: lo que el vendedor ve antes de
/// confirmar, con y sin conexión.
void main() {
  const quotePath = 'api/mobile/v1/sales/quote';

  const yucas = SaleProduct(5, 'Yucas Tostadas', 'Snacks', 1100, 50);
  const agua = SaleProduct(9, 'Agua 600ml', 'Bebidas', 650, 50);

  final quoteJson = {
    'lineas': [
      {
        'productoId': 5,
        'nombre': 'Yucas Tostadas',
        'cantidad': 3,
        'precioLista': 1100.00,
        'montoDescuento': 330.00,
        'precioUnitario': 990.00,
        'subtotal': 2970.00,
        'esRegalo': false,
        'promocion': '10 % Yucas',
      },
      {
        'productoId': 9,
        'nombre': 'Agua 600ml',
        'cantidad': 2,
        'precioLista': 650.00,
        'montoDescuento': 0,
        'precioUnitario': 0,
        'subtotal': 0,
        'esRegalo': true,
        'promocion': 'Regalía por volumen',
      },
    ],
    'subtotalLista': 3300.00,
    'descuento': 330.00,
    'subtotal': 2970.00,
    'impuesto': 386.10,
    'total': 3356.10,
    'umbralRetencion': 50000.00,
    'retenido': false,
  };

  group('SaleQuote.fromJson', () {
    test('lee líneas, descuento, IVA, total y umbral', () {
      final quote = SaleQuote.fromJson(quoteJson);

      expect(quote.lines, hasLength(2));
      expect(quote.listSubtotal, 3300);
      expect(quote.discount, 330);
      expect(quote.subtotal, 2970);
      expect(quote.tax, 386.10);
      expect(quote.total, 3356.10);
      expect(quote.retentionThreshold, 50000);
      expect(quote.retained, isFalse);
      expect(quote.estimated, isFalse);
    });

    test('la línea con descuento trae lista, unitario y promoción', () {
      final line = SaleQuote.fromJson(quoteJson).lines.first;

      expect(line.productId, 5);
      expect(line.name, 'Yucas Tostadas');
      expect(line.quantity, 3);
      expect(line.listPrice, 1100);
      expect(line.discountAmount, 330);
      expect(line.unitPrice, 990);
      expect(line.subtotal, 2970);
      expect(line.promotion, '10 % Yucas');
      expect(line.hasDiscount, isTrue);
      expect(line.isGift, isFalse);
    });

    test('la regalía va a precio cero y no cuenta como descuento', () {
      final gift = SaleQuote.fromJson(quoteJson).lines.last;

      expect(gift.isGift, isTrue);
      expect(gift.unitPrice, 0);
      expect(gift.subtotal, 0);
      expect(gift.promotion, 'Regalía por volumen');
      expect(gift.hasDiscount, isFalse);
    });

    test('retenido llega como bandera', () {
      final quote = SaleQuote.fromJson({...quoteJson, 'retenido': true, 'total': 60000});

      expect(quote.retained, isTrue);
    });

    test('un campo ausente no tumba la lectura', () {
      final quote = SaleQuote.fromJson(const {});

      expect(quote.lines, isEmpty);
      expect(quote.total, 0);
      expect(quote.retained, isFalse);
    });
  });

  group('IVA local (13 %, 2 decimales, mitad hacia arriba)', () {
    test('cálculos exactos', () {
      expect(SalePricing.tax(2970), 386.10);
      expect(SalePricing.totalWithTax(2970), 3356.10);
      expect(SalePricing.tax(1000), 130);
      expect(SalePricing.totalWithTax(0), 0);
    });

    test('la mitad de un céntimo sube', () {
      // 0,50 * 13 % = 0,065 -> 0,07 (y no 0,06).
      expect(SalePricing.tax(0.50), 0.07);
      // 1,50 * 13 % = 0,195 -> 0,20.
      expect(SalePricing.tax(1.50), 0.20);
      // 0,10 * 13 % = 0,013 -> 0,01.
      expect(SalePricing.tax(0.10), 0.01);
    });

    test('el total es subtotal más IVA ya redondeado', () {
      expect(SalePricing.totalWithTax(1.50), 1.70);
    });
  });

  group('estimado sin conexión', () {
    test('suma a precio de lista, sin descuento, y le calcula el IVA', () {
      final estimate = SalesApi.estimate({yucas: 3, agua: 2});

      expect(estimate.estimated, isTrue);
      expect(estimate.discount, 0);
      expect(estimate.listSubtotal, 4600); // 3300 + 1300
      expect(estimate.subtotal, 4600);
      expect(estimate.tax, 598);
      expect(estimate.total, 5198);
      expect(estimate.retained, isFalse);
    });

    test('conserva una línea por producto, a precio de lista', () {
      final estimate = SalesApi.estimate({yucas: 3});
      final line = estimate.lines.single;

      expect(line.productId, 5);
      expect(line.quantity, 3);
      expect(line.unitPrice, 1100);
      expect(line.subtotal, 3300);
      expect(line.hasDiscount, isFalse);
      expect(line.isGift, isFalse);
    });

    test('un carrito vacío da cero', () {
      final estimate = SalesApi.estimate({});

      expect(estimate.lines, isEmpty);
      expect(estimate.total, 0);
    });

    test('precios con decimales no acumulan error de coma flotante', () {
      const caro = SaleProduct(1, 'Con decimales', 'X', 0.1, 99);
      final estimate = SalesApi.estimate({caro: 3});

      expect(estimate.subtotal, 0.30);
      expect(estimate.tax, 0.04); // 0,039 -> 0,04
      expect(estimate.total, 0.34);
    });
  });

  group('SalesApi.quote', () {
    late _MockApi api;
    late SalesApi sales;

    setUp(() {
      api = _MockApi();
      sales = SalesApi(api: api, sync: _MockSync());
    });

    test('envía cliente y productos, y lee la respuesta', () async {
      when(() => api.post<Map<String, dynamic>>(quotePath, body: any(named: 'body')))
          .thenAnswer((_) async => Map<String, dynamic>.of(quoteJson));

      final quote = await sales.quote(clientId: 123, items: {yucas: 3});

      expect(quote.total, 3356.10);
      final body = verify(() => api.post<Map<String, dynamic>>(quotePath, body: captureAny(named: 'body'))).captured.single
          as Map<String, dynamic>;
      expect(body['clienteId'], 123);
      expect(body['items'], [
        {'productoId': 5, 'cantidad': 3},
      ]);
    });

    test('sin cliente elegido se omite clienteId', () async {
      when(() => api.post<Map<String, dynamic>>(quotePath, body: any(named: 'body')))
          .thenAnswer((_) async => Map<String, dynamic>.of(quoteJson));

      await sales.quote(items: {yucas: 1});

      final body = verify(() => api.post<Map<String, dynamic>>(quotePath, body: captureAny(named: 'body'))).captured.single
          as Map<String, dynamic>;
      expect(body.containsKey('clienteId'), isFalse);
    });

    test('una falla llega a la pantalla para que use el estimado', () async {
      when(() => api.post<Map<String, dynamic>>(quotePath, body: any(named: 'body')))
          .thenThrow(ApiException(ApiFailure.sinConexion, 'Sin conexión'));

      expect(() => sales.quote(items: {yucas: 1}), throwsA(isA<ApiException>()));
    });
  });

  group('OrderDetail con subtotal e IVA', () {
    final detailJson = {
      'pedidoId': 7,
      'cliente': 'Bar El Farolito',
      'estado': 'Pendiente',
      'total': 3356.10,
      'subtotal': 2970.00,
      'impuesto': 386.10,
      'lineas': const [],
    };

    test('lee subtotal e impuesto y muestra el desglose', () {
      final detail = OrderDetail.fromJson(detailJson);

      expect(detail.total, 3356.10);
      expect(detail.subtotal, 2970);
      expect(detail.tax, 386.10);
      expect(detail.hasTaxBreakdown, isTrue);
    });

    test('si no vienen, queda solo el total como antes', () {
      final detail = OrderDetail.fromJson({...detailJson}..remove('subtotal')..remove('impuesto'));

      expect(detail.subtotal, 0);
      expect(detail.tax, 0);
      expect(detail.hasTaxBreakdown, isFalse);
      expect(detail.total, 3356.10);
    });

    test('ceros con total mayor que cero tampoco muestran desglose', () {
      final detail = OrderDetail.fromJson({...detailJson, 'subtotal': 0, 'impuesto': 0});

      expect(detail.hasTaxBreakdown, isFalse);
    });
  });
}
