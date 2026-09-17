import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/core/network/api_exception.dart';
import 'package:proyecto_movil/core/widgets/ui_kit.dart';
import 'package:proyecto_movil/features/auth/domain/session.dart';
import 'package:proyecto_movil/features/management/data/management_api.dart';
import 'package:proyecto_movil/features/office/data/office_api.dart';
import 'package:proyecto_movil/features/warehouse/data/warehouse_api.dart';
import 'package:proyecto_movil/shared/utils/formatters.dart';

/// Aplicación por rol: contratos con la API y reglas que las pantallas usan
/// para decidir qué mostrar.
void main() {
  group('Módulos con sección', () {
    test('la sección y la descripción sobreviven al guardado en el teléfono', () {
      const module = AppModule(
        key: 'warehouse.picking',
        title: 'Pedidos por preparar',
        icon: 'picking',
        enabled: true,
        section: 'Bodega',
        description: 'Alistá los pedidos',
      );

      final restored = AppModule.fromJson(module.toJson());
      expect(restored.section, 'Bodega');
      expect(restored.description, 'Alistá los pedidos');
    });

    test('capacidades guardadas por la versión anterior siguen leyéndose', () {
      final old = AppModule.fromJson({'key': 'driver.routes', 'title': 'Mis rutas', 'icon': 'route', 'enabled': true});
      expect(old.section, isEmpty);
      expect(old.enabled, isTrue);
    });

    test('un administrador puede todo; otro perfil solo lo que tiene', () {
      const admin = Session(userId: 1, fullName: 'A', email: 'a@x.test', role: 'Administrador');
      const bodega = Session(userId: 2, fullName: 'B', email: 'b@x.test', role: 'Bodeguero', permisos: ['INVENTARIO_VER']);

      expect(admin.puede('INVENTARIO_EDITAR'), isTrue);
      expect(bodega.puede('INVENTARIO_VER'), isTrue);
      expect(bodega.puede('INVENTARIO_EDITAR'), isFalse);
    });
  });

  group('Contratos de la API', () {
    test('un pedido trae las transiciones que la base permite', () {
      final order = OrderDetail.fromJson({
        'pedidoId': 12,
        'cliente': 'Bar El Farolito',
        'estado': 'Pendiente',
        'tieneFactura': true,
        'transicionesPermitidas': ['Entregado'],
        'lineas': [
          {'nombre': 'Coca-Cola 2.5L', 'cantidad': 3, 'precioUnitario': 1850, 'subtotal': 5550, 'stockActual': 35},
        ],
      });

      expect(order.allowedTransitions, ['Entregado']);
      expect(order.lines.single.quantity, 3);
      expect(order.invoiced, isTrue);
    });

    test('campos ausentes no rompen la lectura', () {
      final product = Product.fromJson({'productoId': 2});
      expect(product.name, isEmpty);
      expect(product.stock, 0);
      expect(product.active, isFalse);
    });

    test('lo pendiente de una línea de compra nunca es negativo', () {
      final line = PurchaseOrderLine.fromJson({'cantidadOrdenada': 10, 'cantidadRecibida': 12});
      expect(line.pending, 0);
    });

    test('la jornada conserva su versión para resolverla sin pisar cambios', () {
      final attendance = Attendance.fromJson({'jornadaId': 5, 'estado': 'Enviada', 'version': 'AAAAAAAAB9E='});
      expect(attendance.version, 'AAAAAAAAB9E=');
    });

    test('la bitácora distingue lo que entró desde el teléfono', () {
      expect(AuditEntry.fromJson({'descripcion': '[Móvil] Pedido #9 preparado'}).fromMobile, isTrue);
      expect(AuditEntry.fromJson({'descripcion': 'Se editó el producto'}).fromMobile, isFalse);
    });
  });

  group('Presentación', () {
    test('los montos van con el colón adelante', () {
      expect(Fmt.money(184500), '₡184.500');
    });

    test('los estados de la base se muestran legibles', () {
      expect(Fmt.status('EnProceso'), 'En proceso');
      expect(Fmt.status('CerradaConDiscrepancia'), 'Con discrepancia');
      expect(Fmt.status('Entregado'), 'Entregado');
    });

    test('un error de regla de negocio muestra el mensaje del servidor', () {
      final error = ApiException(ApiFailure.reglaDeNegocio, 'No hay suficiente stock para esa salida.');
      expect(friendlyError(error), 'No hay suficiente stock para esa salida.');
    });

    test('un 403 no culpa a la red', () {
      final error = ApiException(ApiFailure.sinPermiso, 'permiso_insuficiente');
      expect(friendlyError(error), contains('permiso'));
      expect(friendlyError(error), isNot(contains('conexión')));
    });
  });
}
