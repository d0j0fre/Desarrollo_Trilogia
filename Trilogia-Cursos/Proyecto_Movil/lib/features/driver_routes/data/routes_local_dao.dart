import 'package:sqflite/sqflite.dart';

import '../../../core/storage/app_database.dart';
import '../domain/delivery.dart';
import '../domain/route.dart';

/// Cache local de rutas y entregas.
///
/// La interfaz lee siempre de aqui. La red no alimenta a la pantalla: alimenta
/// a esta tabla, y la pantalla reacciona. Esa inversion es lo que hace que la
/// aplicacion se vea igual con señal y sin ella.
class RoutesLocalDao {
  RoutesLocalDao(this._database);

  final AppDatabase _database;
  Database get _db => _database.raw;

  Future<void> saveRoutes(List<DriverRoute> routes) async {
    final ahora = DateTime.now().toIso8601String();

    await _db.transaction((txn) async {
      // Las rutas que ya no vienen del servidor se quitaron del chofer: no
      // deben seguir apareciendo en su telefono.
      final vigentes = routes.map((route) => route.rutaId).toList();
      if (vigentes.isEmpty) {
        await txn.delete('rutas');
      } else {
        final marcadores = List.filled(vigentes.length, '?').join(',');
        await txn.delete(
          'rutas',
          where: 'ruta_id NOT IN ($marcadores)',
          whereArgs: vigentes,
        );
      }

      for (final route in routes) {
        await txn.insert(
          'rutas',
          {
            'ruta_id': route.rutaId,
            'codigo': route.codigo,
            'zona': route.zona,
            'estado': route.estado,
            'vehiculo_placa': route.vehiculoPlaca,
            'fecha_despacho': route.fechaDespacho?.toIso8601String(),
            'total_pedidos': route.totalPedidos,
            'pendientes': route.pendientes,
            'entregados': route.entregados,
            'sincronizado_en': ahora,
          },
          conflictAlgorithm: ConflictAlgorithm.replace,
        );
      }
    });
  }

  Future<List<DriverRoute>> readRoutes() async {
    final rows = await _db.query('rutas', orderBy: 'estado ASC, ruta_id DESC');
    return rows.map(_routeFromRow).toList();
  }

  Future<void> saveRouteDetail(DriverRoute route) async {
    final ahora = DateTime.now().toIso8601String();

    await _db.transaction((txn) async {
      await txn.insert(
        'rutas',
        {
          'ruta_id': route.rutaId,
          'codigo': route.codigo,
          'zona': route.zona,
          'estado': route.estado,
          'vehiculo_placa': route.vehiculoPlaca,
          'fecha_despacho': route.fechaDespacho?.toIso8601String(),
          'total_pedidos': route.entregas.length,
          'pendientes': route.entregas.where((d) => !d.estaCerrada).length,
          'entregados': route.entregas
              .where((d) => d.estadoEntrega == DeliveryStatus.entregado)
              .length,
          'sincronizado_en': ahora,
        },
        conflictAlgorithm: ConflictAlgorithm.replace,
      );

      // Solo se reemplazan las entregas que el servidor todavia ve como
      // abiertas. Si el chofer marco una sin señal, su estado local es mas
      // reciente que el del servidor y no se debe pisar: esa marca todavia
      // esta en la cola esperando salir.
      final pendientesLocales = await txn.query(
        'entregas',
        columns: ['ruta_pedido_id', 'estado_entrega'],
        where: 'ruta_id = ?',
        whereArgs: [route.rutaId],
      );

      final marcadasLocalmente = {
        for (final row in pendientesLocales)
          row['ruta_pedido_id'] as int: row['estado_entrega'] as String,
      };

      await txn.delete('entregas', where: 'ruta_id = ?', whereArgs: [route.rutaId]);

      for (final delivery in route.entregas) {
        final local = marcadasLocalmente[delivery.rutaPedidoId];
        final estadoFinal = _estadoMasReciente(delivery.estadoEntrega, local);

        await txn.insert('entregas', {
          'ruta_pedido_id': delivery.rutaPedidoId,
          'ruta_id': route.rutaId,
          'pedido_id': delivery.pedidoId,
          'secuencia': delivery.secuencia,
          'estado_entrega': estadoFinal,
          'motivo_fallo': delivery.motivoFallo,
          'fecha_entrega': delivery.fechaEntrega?.toIso8601String(),
          'cliente': delivery.cliente,
          'telefono': delivery.telefono,
          'direccion': delivery.direccion,
          'total': delivery.total,
          'latitud': delivery.latitud,
          'longitud': delivery.longitud,
          'sincronizado_en': ahora,
        });
      }
    });
  }

  /// Un estado cerrado local le gana a uno abierto del servidor: significa que
  /// el chofer ya marco la entrega y todavia no se ha sincronizado.
  static String _estadoMasReciente(String servidor, String? local) {
    if (local == null) return servidor;

    const cerrados = {DeliveryStatus.entregado, DeliveryStatus.fallido};
    if (cerrados.contains(local) && !cerrados.contains(servidor)) return local;

    return servidor;
  }

  Future<DriverRoute?> readRouteDetail(int rutaId) async {
    final routeRows = await _db.query(
      'rutas',
      where: 'ruta_id = ?',
      whereArgs: [rutaId],
      limit: 1,
    );
    if (routeRows.isEmpty) return null;

    final deliveryRows = await _db.query(
      'entregas',
      where: 'ruta_id = ?',
      whereArgs: [rutaId],
      orderBy: 'secuencia ASC, ruta_pedido_id ASC',
    );

    final base = _routeFromRow(routeRows.first);

    return DriverRoute(
      rutaId: base.rutaId,
      codigo: base.codigo,
      zona: base.zona,
      estado: base.estado,
      vehiculoPlaca: base.vehiculoPlaca,
      fechaDespacho: base.fechaDespacho,
      totalPedidos: base.totalPedidos,
      pendientes: base.pendientes,
      entregados: base.entregados,
      sincronizadoEn: base.sincronizadoEn,
      entregas: deliveryRows.map(_deliveryFromRow).toList(),
    );
  }

  /// Aplica el cambio en local de inmediato. Es lo que hace que el chofer vea
  /// "Entregado" sin esperar a la red.
  Future<void> applyLocalStatus({
    required int rutaPedidoId,
    required String estado,
    String motivoFallo = '',
  }) async {
    await _db.update(
      'entregas',
      {
        'estado_entrega': estado,
        'motivo_fallo': motivoFallo,
        'fecha_entrega': DateTime.now().toIso8601String(),
      },
      where: 'ruta_pedido_id = ?',
      whereArgs: [rutaPedidoId],
    );
  }

  static DriverRoute _routeFromRow(Map<String, Object?> row) => DriverRoute(
        rutaId: row['ruta_id']! as int,
        codigo: row['codigo']! as String,
        zona: row['zona']! as String,
        estado: row['estado']! as String,
        vehiculoPlaca: (row['vehiculo_placa'] as String?) ?? '',
        fechaDespacho: row['fecha_despacho'] == null
            ? null
            : DateTime.tryParse(row['fecha_despacho']! as String),
        totalPedidos: (row['total_pedidos'] as int?) ?? 0,
        pendientes: (row['pendientes'] as int?) ?? 0,
        entregados: (row['entregados'] as int?) ?? 0,
        sincronizadoEn: DateTime.tryParse(row['sincronizado_en']! as String),
      );

  static Delivery _deliveryFromRow(Map<String, Object?> row) => Delivery(
        rutaPedidoId: row['ruta_pedido_id']! as int,
        pedidoId: row['pedido_id']! as int,
        secuencia: (row['secuencia'] as int?) ?? 0,
        estadoEntrega: row['estado_entrega']! as String,
        motivoFallo: (row['motivo_fallo'] as String?) ?? '',
        fechaEntrega: row['fecha_entrega'] == null
            ? null
            : DateTime.tryParse(row['fecha_entrega']! as String),
        cliente: (row['cliente'] as String?) ?? '',
        telefono: (row['telefono'] as String?) ?? '',
        direccion: (row['direccion'] as String?) ?? '',
        total: (row['total'] as num?)?.toDouble() ?? 0,
        latitud: (row['latitud'] as num?)?.toDouble(),
        longitud: (row['longitud'] as num?)?.toDouble(),
      );
}
