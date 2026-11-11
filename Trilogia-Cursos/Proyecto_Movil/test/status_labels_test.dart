import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/core/format/status_labels.dart';

void main() {
  group('statusLabel', () {
    test('estados conocidos se muestran legibles', () {
      expect(statusLabel('EnRuta'), 'En ruta');
      expect(statusLabel('EnProceso'), 'En proceso');
      expect(statusLabel('RecibidaParcial'), 'Recibida parcial');
      expect(statusLabel('CerradaConDiscrepancia'), 'Cerrada con discrepancia');
      expect(statusLabel('Fallido'), 'No entregado');
    });

    test('un estado desconocido en PascalCase se separa en palabras', () {
      expect(statusLabel('PagoParcialPendiente'), 'Pago parcial pendiente');
    });

    test('estados simples o vacíos no cambian', () {
      expect(statusLabel('Entregado'), 'Entregado');
      expect(statusLabel('En proceso'), 'En proceso');
      expect(statusLabel(''), '');
    });
  });
}
