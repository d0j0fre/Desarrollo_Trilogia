import 'dart:convert';

import 'package:sqflite/sqflite.dart';

import '../../../core/storage/app_database.dart';

/// Ultima lista de clientes y productos que respondio el servidor.
///
/// La venta en campo la necesita para tomar un pedido sin señal: el pedido ya
/// viajaba por la cola, pero sin clientes no habia forma de armarlo.
abstract class SalesCatalogCache {
  Future<void> save(String key, List<dynamic> items);

  /// `null` si nunca se descargo esa lista en este telefono.
  Future<List<dynamic>?> read(String key);
}

class SqliteSalesCatalogCache implements SalesCatalogCache {
  SqliteSalesCatalogCache(this._database);

  final AppDatabase _database;

  @override
  Future<void> save(String key, List<dynamic> items) async {
    await _database.raw.insert(
      'catalogo_ventas',
      {
        'clave': key,
        'datos': jsonEncode(items),
        'sincronizado_en': DateTime.now().toIso8601String(),
      },
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  @override
  Future<List<dynamic>?> read(String key) async {
    final rows = await _database.raw.query(
      'catalogo_ventas',
      columns: ['datos'],
      where: 'clave = ?',
      whereArgs: [key],
    );
    if (rows.isEmpty) return null;
    return jsonDecode(rows.first['datos'] as String) as List<dynamic>;
  }
}
