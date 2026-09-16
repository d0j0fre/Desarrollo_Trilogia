import 'package:flutter/foundation.dart';

/// Quien esta usando la aplicacion y que puede hacer.
///
/// `permisos` y `modulos` llegan del servidor en cada arranque. La aplicacion
/// los usa para decidir que dibujar, nunca para decidir accesos: eso lo vuelve
/// a resolver la API en cada peticion. Ocultar el boton y dejar el endpoint
/// abierto es el error clasico de las aplicaciones con token.
@immutable
class Session {
  const Session({
    required this.userId,
    required this.fullName,
    required this.email,
    required this.role,
    this.permisos = const [],
    this.modulos = const [],
  });

  final int userId;
  final String fullName;
  final String email;
  final String role;
  final List<String> permisos;
  final List<AppModule> modulos;

  bool get esChofer => role.toLowerCase() == 'chofer';
  bool get esAdministrador => role.toLowerCase() == 'administrador';

  bool puede(String codigo) => esAdministrador || permisos.contains(codigo);

  bool moduloHabilitado(String key) =>
      modulos.any((modulo) => modulo.key == key && modulo.enabled);

  Session copyWith({List<String>? permisos, List<AppModule>? modulos}) => Session(
        userId: userId,
        fullName: fullName,
        email: email,
        role: role,
        permisos: permisos ?? this.permisos,
        modulos: modulos ?? this.modulos,
      );
}

@immutable
class AppModule {
  const AppModule({
    required this.key,
    required this.title,
    required this.icon,
    required this.enabled,
    this.section = '',
    this.description = '',
  });

  final String key;
  final String title;
  final String icon;
  final bool enabled;

  /// Agrupa la pantalla de inicio (Bodega, Gestión, Oficina…). Vacío en
  /// capacidades guardadas por una versión anterior de la aplicación.
  final String section;
  final String description;

  factory AppModule.fromJson(Map<String, dynamic> json) => AppModule(
        key: json['key'] as String? ?? '',
        title: json['title'] as String? ?? '',
        icon: json['icon'] as String? ?? '',
        enabled: json['enabled'] as bool? ?? false,
        section: json['section'] as String? ?? '',
        description: json['description'] as String? ?? '',
      );

  Map<String, dynamic> toJson() => {
        'key': key,
        'title': title,
        'icon': icon,
        'enabled': enabled,
        'section': section,
        'description': description,
      };
}
