import 'dart:convert';

enum OutboxStatus { pendiente, enviando, confirmado, conflicto }

/// Una accion del chofer esperando salir a la red.
///
/// `syncGuid` es la pieza central: lo genera el telefono, viaja a la API y
/// llega hasta el procedimiento almacenado, que ya sabe reconocerlo. Por eso
/// esta cola puede reintentar cuantas veces haga falta sin duplicar una entrega
/// ni una jornada.
class OutboxEntry {
  const OutboxEntry({
    required this.syncGuid,
    required this.tipo,
    required this.endpoint,
    required this.payload,
    required this.descripcion,
    required this.estado,
    required this.intentos,
    required this.creadoEn,
    this.proximoIntento,
    this.ultimoError,
  });

  final String syncGuid;
  final String tipo;
  final String endpoint;
  final Map<String, dynamic> payload;

  /// Texto que ve el chofer en la lista de pendientes. Se guarda porque el
  /// payload solo tiene identificadores, y "Pedido #412 entregado" es lo unico
  /// que le dice algo a una persona.
  final String descripcion;

  final OutboxStatus estado;
  final int intentos;
  final DateTime creadoEn;
  final DateTime? proximoIntento;
  final String? ultimoError;

  static const int maxIntentos = 10;

  bool get agotado => intentos >= maxIntentos;

  bool listaParaEnviar(DateTime ahora) =>
      estado == OutboxStatus.pendiente &&
      (proximoIntento == null || !proximoIntento!.isAfter(ahora));

  /// Espera antes del proximo intento: 1s, 2s, 4s… con tope de 5 minutos.
  /// El tope importa: sin el, tras unas horas sin señal el siguiente intento
  /// quedaria programado para mañana.
  static Duration esperaPara(int intentos) {
    const tope = Duration(minutes: 5);
    // El clamp acota el desplazamiento para que no desborde, no la espera: se
    // deja por encima del tope a proposito para que el tope sea quien manda.
    final segundos = 1 << intentos.clamp(0, 12);
    final calculada = Duration(seconds: segundos);
    return calculada > tope ? tope : calculada;
  }

  OutboxEntry copyWith({
    OutboxStatus? estado,
    int? intentos,
    DateTime? proximoIntento,
    String? ultimoError,
    bool limpiarProximoIntento = false,
  }) {
    return OutboxEntry(
      syncGuid: syncGuid,
      tipo: tipo,
      endpoint: endpoint,
      payload: payload,
      descripcion: descripcion,
      estado: estado ?? this.estado,
      intentos: intentos ?? this.intentos,
      creadoEn: creadoEn,
      proximoIntento:
          limpiarProximoIntento ? null : (proximoIntento ?? this.proximoIntento),
      ultimoError: ultimoError ?? this.ultimoError,
    );
  }

  Map<String, Object?> toRow() => {
        'sync_guid': syncGuid,
        'tipo': tipo,
        'endpoint': endpoint,
        'payload': jsonEncode(payload),
        'descripcion': descripcion,
        'estado': estado.name,
        'intentos': intentos,
        'creado_en': creadoEn.toIso8601String(),
        'proximo_intento': proximoIntento?.toIso8601String(),
        'ultimo_error': ultimoError,
      };

  factory OutboxEntry.fromRow(Map<String, Object?> row) => OutboxEntry(
        syncGuid: row['sync_guid']! as String,
        tipo: row['tipo']! as String,
        endpoint: row['endpoint']! as String,
        payload: jsonDecode(row['payload']! as String) as Map<String, dynamic>,
        descripcion: (row['descripcion'] as String?) ?? '',
        estado: OutboxStatus.values.firstWhere(
          (status) => status.name == row['estado'],
          orElse: () => OutboxStatus.pendiente,
        ),
        intentos: (row['intentos'] as int?) ?? 0,
        creadoEn: DateTime.parse(row['creado_en']! as String),
        proximoIntento: row['proximo_intento'] == null
            ? null
            : DateTime.tryParse(row['proximo_intento']! as String),
        ultimoError: row['ultimo_error'] as String?,
      );
}
