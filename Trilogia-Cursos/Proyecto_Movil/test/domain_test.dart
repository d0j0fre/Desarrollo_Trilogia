import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/features/auth/domain/session.dart';
import 'package:proyecto_movil/features/driver_routes/domain/delivery.dart';
import 'package:proyecto_movil/features/driver_routes/domain/route.dart';
import 'package:proyecto_movil/features/home/domain/day_summary.dart';

void main() {
  group('Sesion y permisos', () {
    test('el administrador puede todo sin permisos explicitos', () {
      const admin = Session(
        userId: 1,
        fullName: 'Admin',
        email: 'a@b.test',
        role: 'Administrador',
      );

      expect(admin.puede('LO_QUE_SEA'), isTrue);
    });

    test('un chofer solo puede lo que tiene asignado', () {
      const chofer = Session(
        userId: 2,
        fullName: 'Chofer',
        email: 'c@b.test',
        role: 'Chofer',
        permisos: ['FLOTA_KILOMETRAJE_PROPIO'],
      );

      expect(chofer.puede('FLOTA_KILOMETRAJE_PROPIO'), isTrue);
      expect(chofer.puede('FACTURACION_GENERAR'), isFalse);
    });

    test('un modulo apagado no cuenta como habilitado', () {
      const sesion = Session(
        userId: 2,
        fullName: 'Chofer',
        email: 'c@b.test',
        role: 'Chofer',
        modulos: [
          AppModule(key: 'driver.routes', title: 'Rutas', icon: 'route', enabled: true),
          AppModule(key: 'driver.evidence', title: 'Evidencias', icon: 'camera', enabled: false),
        ],
      );

      expect(sesion.moduloHabilitado('driver.routes'), isTrue);
      expect(sesion.moduloHabilitado('driver.evidence'), isFalse);
      expect(sesion.moduloHabilitado('inexistente'), isFalse);
    });
  });

  group('Entrega', () {
    Delivery build({
      String estado = DeliveryStatus.pendiente,
      double? lat,
      double? lng,
    }) =>
        Delivery(
          rutaPedidoId: 1,
          pedidoId: 2,
          secuencia: 1,
          estadoEntrega: estado,
          cliente: 'Cliente',
          telefono: '',
          direccion: '',
          total: 0,
          latitud: lat,
          longitud: lng,
        );

    test('entregada y fallida estan cerradas; pendiente y en ruta no', () {
      expect(build(estado: DeliveryStatus.entregado).estaCerrada, isTrue);
      expect(build(estado: DeliveryStatus.fallido).estaCerrada, isTrue);
      expect(build(estado: DeliveryStatus.pendiente).estaCerrada, isFalse);
      expect(build(estado: DeliveryStatus.enRuta).estaCerrada, isFalse);
    });

    test('coordenadas en cero no cuentan como coordenadas', () {
      // Una fila sin georreferenciar suele llegar con ceros, y mandar al
      // chofer al golfo de Guinea no es util.
      expect(build(lat: 0, lng: 0).tieneCoordenadas, isFalse);
      expect(build().tieneCoordenadas, isFalse);
      expect(build(lat: 9.93, lng: -84.08).tieneCoordenadas, isTrue);
    });

    test('se lee del JSON de la API con los nombres que esta usa', () {
      final delivery = Delivery.fromJson(const {
        'rutaPedidoId': 10,
        'pedidoId': 412,
        'secuencia': 3,
        'estadoEntrega': 'Entregado',
        'cliente': 'Pulpería La Esquina',
        'telefono': '8888-8888',
        'direccionEntrega': 'Calle 5',
        'total': 45000.0,
        'latitud': 9.93,
        'longitud': -84.08,
      });

      expect(delivery.pedidoId, 412);
      expect(delivery.direccion, 'Calle 5');
      expect(delivery.total, 45000.0);
      expect(delivery.estaCerrada, isTrue);
    });
  });

  group('Ruta', () {
    DriverRoute build({int total = 0, int entregados = 0, String estado = 'Despachada'}) =>
        DriverRoute(
          rutaId: 1,
          codigo: 'R-001',
          zona: 'Centro',
          estado: estado,
          vehiculoPlaca: 'ABC123',
          totalPedidos: total,
          entregados: entregados,
        );

    test('el avance de una ruta vacia es cero y no una division por cero', () {
      expect(build().avance, 0);
    });

    test('el avance es la proporcion de entregadas', () {
      expect(build(total: 4, entregados: 1).avance, 0.25);
      expect(build(total: 4, entregados: 4).avance, 1);
    });

    test('planificada y despachada estan activas; completada no', () {
      expect(build(estado: 'Despachada').estaActiva, isTrue);
      expect(build(estado: 'Planificada').estaActiva, isTrue);
      expect(build(estado: 'Completada').estaActiva, isFalse);
      expect(build(estado: 'Cancelada').estaActiva, isFalse);
    });
  });

  group('Compuerta de version', () {
    const gate = AppVersionGate(
      latestBuild: 42,
      latestVersion: '1.3.0',
      minSupportedBuild: 38,
      downloadUrl: 'https://example.test/app',
    );

    test('bloquea por debajo de la version minima', () {
      expect(gate.bloquea(37), isTrue);
      expect(gate.bloquea(38), isFalse);
      expect(gate.bloquea(42), isFalse);
    });

    test('sugiere actualizar sin bloquear entre la minima y la ultima', () {
      expect(gate.sugiereActualizar(38), isTrue);
      expect(gate.bloquea(38), isFalse);
      expect(gate.sugiereActualizar(42), isFalse);
    });

    test('sin ninguna version publicada no bloquea a nadie', () {
      // Dejar a toda la flota fuera porque falta un registro administrativo
      // seria peor que el problema que la compuerta resuelve.
      const vacia = AppVersionGate(
        latestBuild: 0,
        latestVersion: '',
        minSupportedBuild: 0,
        downloadUrl: '',
      );

      expect(vacia.bloquea(1), isFalse);
      expect(vacia.sugiereActualizar(1), isFalse);
    });
  });

  group('Resumen del dia', () {
    test('un JSON incompleto se lee con ceros y no revienta', () {
      final resumen = DaySummary.fromJson(const {});
      expect(resumen.entregasPendientes, 0);
      expect(resumen.jornadaAbierta, isFalse);
    });
  });
}
