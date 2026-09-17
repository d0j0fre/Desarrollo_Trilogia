import 'package:flutter/foundation.dart';

import 'delivery.dart';

@immutable
class DriverRoute {
  const DriverRoute({
    required this.rutaId,
    required this.codigo,
    required this.zona,
    required this.estado,
    required this.vehiculoPlaca,
    this.fechaDespacho,
    this.totalPedidos = 0,
    this.pendientes = 0,
    this.entregados = 0,
    this.entregas = const [],
    this.sincronizadoEn,
  });

  final int rutaId;
  final String codigo;
  final String zona;
  final String estado;
  final String vehiculoPlaca;
  final DateTime? fechaDespacho;
  final int totalPedidos;
  final int pendientes;
  final int entregados;
  final List<Delivery> entregas;

  /// Cuando se descargo. Sirve para avisarle al chofer que esta viendo datos
  /// viejos si lleva horas sin señal.
  final DateTime? sincronizadoEn;

  bool get estaActiva => estado == 'Despachada' || estado == 'Planificada';

  double get avance =>
      totalPedidos == 0 ? 0 : (entregados / totalPedidos).clamp(0, 1);

  factory DriverRoute.fromJson(Map<String, dynamic> json) => DriverRoute(
        rutaId: json['rutaId'] as int,
        codigo: json['codigo'] as String? ?? '',
        zona: json['zona'] as String? ?? '',
        estado: json['estado'] as String? ?? '',
        vehiculoPlaca: json['vehiculoPlaca'] as String? ?? '',
        fechaDespacho: DateTime.tryParse(json['fechaDespacho'] as String? ?? ''),
        totalPedidos: json['totalPedidos'] as int? ?? 0,
        pendientes: json['pendientes'] as int? ?? 0,
        entregados: json['entregados'] as int? ?? 0,
        entregas: (json['entregas'] as List<dynamic>? ?? [])
            .map((item) => Delivery.fromJson(item as Map<String, dynamic>))
            .toList(),
      );
}
