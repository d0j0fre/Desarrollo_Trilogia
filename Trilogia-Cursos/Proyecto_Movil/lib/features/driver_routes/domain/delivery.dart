import 'package:flutter/foundation.dart';

/// Estados que la API acepta. Los nombres son los del procedimiento
/// almacenado: no se traducen aqui para que no haya dos vocabularios.
class DeliveryStatus {
  const DeliveryStatus._();

  static const pendiente = 'Pendiente';
  static const enRuta = 'EnRuta';
  static const entregado = 'Entregado';
  static const fallido = 'Fallido';
}

@immutable
class Delivery {
  const Delivery({
    required this.rutaPedidoId,
    required this.pedidoId,
    required this.secuencia,
    required this.estadoEntrega,
    required this.cliente,
    required this.telefono,
    required this.direccion,
    required this.total,
    this.motivoFallo = '',
    this.fechaEntrega,
    this.latitud,
    this.longitud,
    this.pendienteDeSincronizar = false,
  });

  final int rutaPedidoId;
  final int pedidoId;
  final int secuencia;
  final String estadoEntrega;
  final String motivoFallo;
  final DateTime? fechaEntrega;
  final String cliente;
  final String telefono;
  final String direccion;
  final double total;
  final double? latitud;
  final double? longitud;

  /// El cambio ya se aplico en pantalla pero todavia no salio del telefono.
  /// Se muestra distinto para que el chofer sepa la diferencia.
  final bool pendienteDeSincronizar;

  bool get tieneCoordenadas =>
      latitud != null && longitud != null && (latitud != 0 || longitud != 0);

  bool get estaCerrada =>
      estadoEntrega == DeliveryStatus.entregado ||
      estadoEntrega == DeliveryStatus.fallido;

  Delivery copyWith({
    String? estadoEntrega,
    String? motivoFallo,
    DateTime? fechaEntrega,
    bool? pendienteDeSincronizar,
  }) =>
      Delivery(
        rutaPedidoId: rutaPedidoId,
        pedidoId: pedidoId,
        secuencia: secuencia,
        estadoEntrega: estadoEntrega ?? this.estadoEntrega,
        motivoFallo: motivoFallo ?? this.motivoFallo,
        fechaEntrega: fechaEntrega ?? this.fechaEntrega,
        cliente: cliente,
        telefono: telefono,
        direccion: direccion,
        total: total,
        latitud: latitud,
        longitud: longitud,
        pendienteDeSincronizar:
            pendienteDeSincronizar ?? this.pendienteDeSincronizar,
      );

  factory Delivery.fromJson(Map<String, dynamic> json) => Delivery(
        rutaPedidoId: json['rutaPedidoId'] as int,
        pedidoId: json['pedidoId'] as int,
        secuencia: json['secuencia'] as int? ?? 0,
        estadoEntrega: json['estadoEntrega'] as String? ?? DeliveryStatus.pendiente,
        motivoFallo: json['motivoFallo'] as String? ?? '',
        fechaEntrega: DateTime.tryParse(json['fechaEntrega'] as String? ?? ''),
        cliente: json['cliente'] as String? ?? '',
        telefono: json['telefono'] as String? ?? '',
        direccion: json['direccionEntrega'] as String? ?? '',
        total: (json['total'] as num?)?.toDouble() ?? 0,
        latitud: (json['latitud'] as num?)?.toDouble(),
        longitud: (json['longitud'] as num?)?.toDouble(),
      );
}
