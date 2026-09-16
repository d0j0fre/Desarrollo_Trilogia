import 'package:flutter/foundation.dart';

@immutable
class DaySummary {
  const DaySummary({
    this.rutasActivas = 0,
    this.entregasPendientes = 0,
    this.entregasCompletadasHoy = 0,
    this.entregasFallidasHoy = 0,
    this.jornadaAbierta = false,
  });

  final int rutasActivas;
  final int entregasPendientes;
  final int entregasCompletadasHoy;
  final int entregasFallidasHoy;
  final bool jornadaAbierta;

  factory DaySummary.fromJson(Map<String, dynamic> json) => DaySummary(
        rutasActivas: json['rutasActivas'] as int? ?? 0,
        entregasPendientes: json['entregasPendientes'] as int? ?? 0,
        entregasCompletadasHoy: json['entregasCompletadasHoy'] as int? ?? 0,
        entregasFallidasHoy: json['entregasFallidasHoy'] as int? ?? 0,
        jornadaAbierta: json['jornadaAbierta'] as bool? ?? false,
      );
}

/// Version minima soportada. Sin tienda de aplicaciones esta es la unica forma
/// de retirar del campo una compilacion con un error grave.
@immutable
class AppVersionGate {
  const AppVersionGate({
    required this.latestBuild,
    required this.latestVersion,
    required this.minSupportedBuild,
    required this.downloadUrl,
    this.mandatoryMessage,
  });

  final int latestBuild;
  final String latestVersion;
  final int minSupportedBuild;
  final String downloadUrl;
  final String? mandatoryMessage;

  bool bloquea(int buildInstalado) =>
      minSupportedBuild > 0 && buildInstalado < minSupportedBuild;

  bool sugiereActualizar(int buildInstalado) =>
      latestBuild > 0 && buildInstalado < latestBuild;

  factory AppVersionGate.fromJson(Map<String, dynamic> json) => AppVersionGate(
        latestBuild: json['latestBuild'] as int? ?? 0,
        latestVersion: json['latestVersion'] as String? ?? '',
        minSupportedBuild: json['minSupportedBuild'] as int? ?? 0,
        downloadUrl: json['downloadUrl'] as String? ?? '',
        mandatoryMessage: json['mandatoryMessage'] as String?,
      );
}
