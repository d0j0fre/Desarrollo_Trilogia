import 'package:flutter/foundation.dart';

@immutable
class DriverVehicle {
  const DriverVehicle({
    required this.vehiculoId,
    required this.placa,
    required this.descripcion,
    required this.kilometrajeActual,
    required this.jornadaAbierta,
  });

  final int vehiculoId;
  final String placa;
  final String descripcion;
  final int kilometrajeActual;
  final bool jornadaAbierta;

  factory DriverVehicle.fromJson(Map<String, dynamic> json) => DriverVehicle(
        vehiculoId: json['vehiculoId'] as int,
        placa: json['placa'] as String? ?? '',
        descripcion: json['descripcion'] as String? ?? '',
        kilometrajeActual: json['kilometrajeActual'] as int? ?? 0,
        jornadaAbierta: json['jornadaAbierta'] as bool? ?? false,
      );
}

/// Jornada de kilometraje abierta. Que sea nula es el estado normal de quien
/// todavia no sale a ruta, no un error.
@immutable
class OpenShift {
  const OpenShift({
    required this.kilometrajeId,
    required this.vehiculoId,
    required this.vehiculoPlaca,
    required this.kmInicial,
    required this.abiertaEn,
  });

  final int kilometrajeId;
  final int vehiculoId;
  final String vehiculoPlaca;
  final int kmInicial;
  final DateTime abiertaEn;

  factory OpenShift.fromJson(Map<String, dynamic> json) => OpenShift(
        kilometrajeId: json['kilometrajeId'] as int,
        vehiculoId: json['vehiculoId'] as int,
        vehiculoPlaca: json['vehiculoPlaca'] as String? ?? '',
        kmInicial: json['kmInicial'] as int? ?? 0,
        abiertaEn:
            DateTime.tryParse(json['fechaRegistro'] as String? ?? '') ??
                DateTime.now(),
      );
}
