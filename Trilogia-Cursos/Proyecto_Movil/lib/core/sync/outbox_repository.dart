import 'package:sqflite/sqflite.dart';

import '../storage/app_database.dart';
import 'outbox_entry.dart';

/// Persistencia de la cola.
class OutboxRepository {
  OutboxRepository(this._database);

  final AppDatabase _database;
  Database get _db => _database.raw;

  /// Encola una accion. Si el mismo `syncGuid` ya estaba, no la duplica:
  /// el usuario pudo tocar dos veces el boton.
  Future<void> enqueue(OutboxEntry entry) async {
    await _db.insert(
      'outbox',
      entry.toRow(),
      conflictAlgorithm: ConflictAlgorithm.ignore,
    );
  }

  Future<List<OutboxEntry>> pendientes() async {
    final rows = await _db.query(
      'outbox',
      where: 'estado = ?',
      whereArgs: [OutboxStatus.pendiente.name],
      orderBy: 'creado_en ASC',
    );
    return rows.map(OutboxEntry.fromRow).toList();
  }

  Future<OutboxEntry?> porGuid(String syncGuid) async {
    final rows = await _db.query(
      'outbox',
      where: 'sync_guid = ?',
      whereArgs: [syncGuid],
      limit: 1,
    );
    return rows.isEmpty ? null : OutboxEntry.fromRow(rows.first);
  }

  /// Si la accion sigue en la cola, pendiente o con conflicto.
  Future<bool> existe(String syncGuid) async => await porGuid(syncGuid) != null;

  /// Todas las entradas, sin importar su estado. Sirve para saber que archivos
  /// de evidencia siguen haciendo falta.
  Future<List<OutboxEntry>> todas() async {
    final rows = await _db.query('outbox', orderBy: 'creado_en ASC');
    return rows.map(OutboxEntry.fromRow).toList();
  }

  /// Las acciones que esperan a otra (la evidencia espera al estado de su
  /// entrega). El campo viaja dentro del payload, asi que no hace falta una
  /// columna nueva ni cambiar la version de la base.
  Future<List<OutboxEntry>> dependientesDe(String syncGuid) async =>
      (await todas()).where((entry) => entry.dependeDe == syncGuid).toList();

  Future<List<OutboxEntry>> conflictos() async {
    final rows = await _db.query(
      'outbox',
      where: 'estado = ?',
      whereArgs: [OutboxStatus.conflicto.name],
      orderBy: 'creado_en ASC',
    );
    return rows.map(OutboxEntry.fromRow).toList();
  }

  /// Cuantas acciones faltan por salir. Es lo que se muestra en la franja
  /// superior: la transparencia evita el "yo si lo marque".
  Future<int> pendientesCount() async {
    final result = await _db.rawQuery(
      'SELECT COUNT(*) AS total FROM outbox WHERE estado = ?',
      [OutboxStatus.pendiente.name],
    );
    return Sqflite.firstIntValue(result) ?? 0;
  }

  Future<int> conflictosCount() async {
    final result = await _db.rawQuery(
      'SELECT COUNT(*) AS total FROM outbox WHERE estado = ?',
      [OutboxStatus.conflicto.name],
    );
    return Sqflite.firstIntValue(result) ?? 0;
  }

  Future<void> update(OutboxEntry entry) async {
    await _db.update(
      'outbox',
      entry.toRow(),
      where: 'sync_guid = ?',
      whereArgs: [entry.syncGuid],
    );
  }

  /// Una accion confirmada se borra. No se conserva historial local: el
  /// historial real vive en la base del servidor y en la auditoria.
  Future<void> remove(String syncGuid) async {
    await _db.delete('outbox', where: 'sync_guid = ?', whereArgs: [syncGuid]);
  }

  /// Quita la espera de reintento de todo lo pendiente. Se usa al recuperar la
  /// señal: la espera creciente (hasta 5 minutos) existe para no castigar a un
  /// servidor caido, pero con red recien recuperada no tiene sentido hacerle esperar
  /// al chofer lo que acumulo sin señal.
  Future<void> reiniciarEsperas() async {
    await _db.update(
      'outbox',
      {'proximo_intento': null},
      where: 'estado = ?',
      whereArgs: [OutboxStatus.pendiente.name],
    );
  }

  /// Vuelve a poner en cola un conflicto que el chofer decidio reintentar.
  Future<void> reintentar(String syncGuid) async {
    await _db.update(
      'outbox',
      {
        'estado': OutboxStatus.pendiente.name,
        'intentos': 0,
        'proximo_intento': null,
        'ultimo_error': null,
      },
      where: 'sync_guid = ?',
      whereArgs: [syncGuid],
    );
  }
}
