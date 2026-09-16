import 'package:path/path.dart' as p;
import 'package:sqflite/sqflite.dart';

/// Base local.
///
/// Cumple dos papeles y conviene no confundirlos:
///
/// 1. **Cache de lectura.** La interfaz lee siempre de aqui; la red actualiza
///    esta base. Asi nunca hay una pantalla en blanco esperando señal.
/// 2. **Cola de escritura (outbox).** Toda accion del chofer entra aqui antes
///    de salir a la red, con un identificador propio que la vuelve
///    reintentable sin riesgo de duplicar nada.
class AppDatabase {
  AppDatabase._(this._db);

  final Database _db;
  Database get raw => _db;

  static const _fileName = 'operacion_labodega.db';
  static const _version = 1;

  static Future<AppDatabase> open({String? path}) async {
    final databasePath = path ?? p.join(await getDatabasesPath(), _fileName);

    final db = await openDatabase(
      databasePath,
      version: _version,
      onConfigure: (db) => db.execute('PRAGMA foreign_keys = ON'),
      onCreate: _create,
    );

    return AppDatabase._(db);
  }

  static Future<void> _create(Database db, int version) async {
    // Rutas y entregas descargadas. Se guardan tal como llegaron para poder
    // mostrarlas sin red; `sincronizado_en` sirve para avisar cuando lo que se
    // esta viendo ya tiene horas.
    await db.execute('''
      CREATE TABLE rutas (
        ruta_id           INTEGER PRIMARY KEY,
        codigo            TEXT    NOT NULL,
        zona              TEXT    NOT NULL,
        estado            TEXT    NOT NULL,
        vehiculo_placa    TEXT    NOT NULL DEFAULT '',
        fecha_despacho    TEXT,
        total_pedidos     INTEGER NOT NULL DEFAULT 0,
        pendientes        INTEGER NOT NULL DEFAULT 0,
        entregados        INTEGER NOT NULL DEFAULT 0,
        sincronizado_en   TEXT    NOT NULL
      )
    ''');

    await db.execute('''
      CREATE TABLE entregas (
        ruta_pedido_id    INTEGER PRIMARY KEY,
        ruta_id           INTEGER NOT NULL,
        pedido_id         INTEGER NOT NULL,
        secuencia         INTEGER NOT NULL DEFAULT 0,
        estado_entrega    TEXT    NOT NULL,
        motivo_fallo      TEXT    NOT NULL DEFAULT '',
        fecha_entrega     TEXT,
        cliente           TEXT    NOT NULL DEFAULT '',
        telefono          TEXT    NOT NULL DEFAULT '',
        direccion         TEXT    NOT NULL DEFAULT '',
        total             REAL    NOT NULL DEFAULT 0,
        latitud           REAL,
        longitud          REAL,
        sincronizado_en   TEXT    NOT NULL,
        FOREIGN KEY (ruta_id) REFERENCES rutas (ruta_id) ON DELETE CASCADE
      )
    ''');

    await db.execute('CREATE INDEX ix_entregas_ruta ON entregas (ruta_id, secuencia)');

    // La cola. `sync_guid` es el mismo identificador de punta a punta:
    // aplicacion -> API -> procedimiento almacenado. Por eso los reintentos son
    // seguros y la duplicacion es imposible por construccion, no por suerte.
    await db.execute('''
      CREATE TABLE outbox (
        sync_guid         TEXT    PRIMARY KEY,
        tipo              TEXT    NOT NULL,
        endpoint          TEXT    NOT NULL,
        payload           TEXT    NOT NULL,
        descripcion       TEXT    NOT NULL DEFAULT '',
        estado            TEXT    NOT NULL DEFAULT 'pendiente',
        intentos          INTEGER NOT NULL DEFAULT 0,
        creado_en         TEXT    NOT NULL,
        proximo_intento   TEXT,
        ultimo_error      TEXT
      )
    ''');

    await db.execute('CREATE INDEX ix_outbox_estado ON outbox (estado, proximo_intento)');

    // Jornada de kilometraje abierta, para saber al arrancar si toca abrir o
    // cerrar sin depender de la red.
    await db.execute('''
      CREATE TABLE jornada (
        id                INTEGER PRIMARY KEY CHECK (id = 1),
        kilometraje_id    INTEGER,
        vehiculo_id       INTEGER,
        vehiculo_placa    TEXT,
        km_inicial        INTEGER,
        abierta_en        TEXT
      )
    ''');
  }

  /// Borra los datos del usuario que cerro sesion. Un telefono se pierde: no
  /// puede quedar la ruta de nadie despues de salir.
  ///
  /// La cola se borra tambien, a proposito. Si quedaran acciones sin enviar de
  /// un usuario y entrara otro, se enviarian con el token del segundo y la
  /// bitacora registraria al chofer equivocado.
  Future<void> clearUserData() async {
    await _db.transaction((txn) async {
      await txn.delete('entregas');
      await txn.delete('rutas');
      await txn.delete('outbox');
      await txn.delete('jornada');
    });
  }

  Future<void> close() => _db.close();
}
