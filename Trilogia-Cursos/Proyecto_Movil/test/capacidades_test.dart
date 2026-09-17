import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/features/auth/application/auth_controller.dart';
import 'package:proyecto_movil/features/auth/domain/session.dart';

/// Lo que puede hacer una persona se guarda en el teléfono y se distingue de
/// "todavía no lo sé".
///
/// Sin esta distinción, la pantalla de inicio le dice a un chofer que su perfil
/// no tiene acceso a nada cuando lo único que pasa es que falta señal. Es
/// alarmante, es falso, y lo manda a hablar con administración por nada.
void main() {
  group('Estado de las capacidades', () {
    test('recién abierta la aplicación, todavía no se sabe', () {
      const estado = AuthState();
      expect(estado.capacidadesCargadas, isFalse);
    });

    test('una sesión restaurada sin nada guardado sigue sin saberse', () {
      const estado = AuthState(
        status: AuthStatus.autenticado,
        session: Session(userId: 1, fullName: 'Chofer', email: 'c@x.test', role: 'Chofer'),
      );

      expect(estado.session!.modulos, isEmpty);
      expect(estado.capacidadesCargadas, isFalse,
          reason: 'lista vacía sin marca no puede interpretarse como "sin permisos"');
    });

    test('con la marca puesta, una lista vacía sí significa sin permisos', () {
      const estado = AuthState(
        status: AuthStatus.autenticado,
        session: Session(userId: 1, fullName: 'Chofer', email: 'c@x.test', role: 'Chofer'),
        capacidadesCargadas: true,
      );

      expect(estado.capacidadesCargadas, isTrue);
      expect(estado.session!.modulos, isEmpty);
    });

    test('copyWith conserva la marca si no se la cambia', () {
      const inicial = AuthState(capacidadesCargadas: true);
      final despues = inicial.copyWith(cargando: true);

      expect(despues.capacidadesCargadas, isTrue);
    });
  });

  group('Guardado de las capacidades', () {
    test('sobreviven la ida y vuelta a texto sin perder nada', () {
      const modulos = [
        AppModule(key: 'driver.routes', title: 'Mis rutas', icon: 'route', enabled: true),
        AppModule(key: 'driver.evidence', title: 'Evidencias', icon: 'camera', enabled: false),
      ];

      final guardado = jsonEncode({
        'permissions': ['MOVIL_ACCESO', 'FLOTA_KILOMETRAJE_PROPIO'],
        'modules': modulos.map((m) => m.toJson()).toList(),
      });

      final leido = jsonDecode(guardado) as Map<String, dynamic>;
      final permisos =
          (leido['permissions'] as List<dynamic>).map((e) => e.toString()).toList();
      final recuperados = (leido['modules'] as List<dynamic>)
          .map((e) => AppModule.fromJson(e as Map<String, dynamic>))
          .toList();

      expect(permisos, containsAll(['MOVIL_ACCESO', 'FLOTA_KILOMETRAJE_PROPIO']));
      expect(recuperados.length, 2);
      expect(recuperados.first.key, 'driver.routes');
      expect(recuperados.first.enabled, isTrue);
      expect(recuperados.last.enabled, isFalse,
          reason: 'un módulo apagado no puede volver encendido al recuperarse');
    });

    test('la sesión recuperada resuelve permisos igual que la original', () {
      const original = Session(
        userId: 7,
        fullName: 'Chofer',
        email: 'c@x.test',
        role: 'Chofer',
        permisos: ['FLOTA_KILOMETRAJE_PROPIO'],
        modulos: [
          AppModule(key: 'driver.mileage', title: 'Kilometraje', icon: 'speedometer', enabled: true),
        ],
      );

      final texto = jsonEncode({
        'permissions': original.permisos,
        'modules': original.modulos.map((m) => m.toJson()).toList(),
      });
      final leido = jsonDecode(texto) as Map<String, dynamic>;

      final recuperada = const Session(
        userId: 7, fullName: 'Chofer', email: 'c@x.test', role: 'Chofer',
      ).copyWith(
        permisos: (leido['permissions'] as List<dynamic>).map((e) => e.toString()).toList(),
        modulos: (leido['modules'] as List<dynamic>)
            .map((e) => AppModule.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

      expect(recuperada.puede('FLOTA_KILOMETRAJE_PROPIO'), isTrue);
      expect(recuperada.puede('FACTURACION_GENERAR'), isFalse);
      expect(recuperada.moduloHabilitado('driver.mileage'), isTrue);
    });
  });
}
