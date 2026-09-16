import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/core/network/api_exception.dart';
import 'package:proyecto_movil/core/sync/outbox_entry.dart';

/// La cola es lo que hace utilizable la aplicacion sin señal. Estas pruebas
/// fijan las dos reglas de las que depende todo lo demas: que el reintento sea
/// seguro y que un conflicto no se reintente solo.
void main() {
  group('Espera entre reintentos', () {
    test('crece exponencialmente desde un segundo', () {
      expect(OutboxEntry.esperaPara(0), const Duration(seconds: 1));
      expect(OutboxEntry.esperaPara(1), const Duration(seconds: 2));
      expect(OutboxEntry.esperaPara(2), const Duration(seconds: 4));
      expect(OutboxEntry.esperaPara(3), const Duration(seconds: 8));
    });

    test('nunca supera cinco minutos', () {
      // Sin tope, tras unas horas sin señal el proximo intento quedaria
      // programado para el dia siguiente.
      expect(OutboxEntry.esperaPara(20), const Duration(minutes: 5));
      expect(OutboxEntry.esperaPara(100), const Duration(minutes: 5));
    });
  });

  group('Entrada de la cola', () {
    final base = OutboxEntry(
      syncGuid: 'a1b2c3',
      tipo: 'entrega',
      endpoint: 'api/mobile/v1/driver/deliveries/1/status',
      payload: const {'estado': 'Entregado'},
      descripcion: 'Pedido #1 entregado',
      estado: OutboxStatus.pendiente,
      intentos: 0,
      creadoEn: DateTime(2026, 9, 15, 8),
    );

    test('esta lista cuando no tiene proximo intento programado', () {
      expect(base.listaParaEnviar(DateTime(2026, 9, 15, 8, 1)), isTrue);
    });

    test('espera si el proximo intento todavia no llega', () {
      final conEspera =
          base.copyWith(proximoIntento: DateTime(2026, 9, 15, 8, 5));
      expect(conEspera.listaParaEnviar(DateTime(2026, 9, 15, 8, 1)), isFalse);
      expect(conEspera.listaParaEnviar(DateTime(2026, 9, 15, 8, 6)), isTrue);
    });

    test('un conflicto no se envia aunque haya pasado la espera', () {
      final conflicto = base.copyWith(estado: OutboxStatus.conflicto);
      expect(conflicto.listaParaEnviar(DateTime(2026, 9, 15, 23)), isFalse);
    });

    test('sobrevive la ida y vuelta a la base sin perder nada', () {
      final recuperada = OutboxEntry.fromRow(base.toRow());

      expect(recuperada.syncGuid, base.syncGuid);
      expect(recuperada.endpoint, base.endpoint);
      expect(recuperada.payload, base.payload);
      expect(recuperada.descripcion, base.descripcion);
      expect(recuperada.estado, base.estado);
      expect(recuperada.creadoEn, base.creadoEn);
    });

    test('se rinde a los diez intentos', () {
      expect(base.copyWith(intentos: 9).agotado, isFalse);
      expect(base.copyWith(intentos: 10).agotado, isTrue);
    });
  });

  group('Clasificacion de fallas', () {
    test('las de red se reintentan', () {
      for (final failure in [
        ApiFailure.sinConexion,
        ApiFailure.tiempoAgotado,
        ApiFailure.servidor,
        ApiFailure.demasiadasPeticiones,
      ]) {
        expect(
          ApiException(failure, 'x').esReintentable,
          isTrue,
          reason: '$failure deberia reintentarse',
        );
      }
    });

    test('las de negocio y de permiso no se reintentan', () {
      // Insistir no va a arreglar que la oficina haya cancelado el pedido.
      for (final failure in [
        ApiFailure.reglaDeNegocio,
        ApiFailure.noEncontrado,
        ApiFailure.sinPermiso,
        ApiFailure.sesionExpirada,
      ]) {
        expect(
          ApiException(failure, 'x').esReintentable,
          isFalse,
          reason: '$failure no deberia reintentarse',
        );
      }
    });

    test('regla de negocio y no encontrado son conflictos para el usuario', () {
      expect(ApiException(ApiFailure.reglaDeNegocio, 'x').esConflicto, isTrue);
      expect(ApiException(ApiFailure.noEncontrado, 'x').esConflicto, isTrue);
      expect(ApiException(ApiFailure.sinConexion, 'x').esConflicto, isFalse);
    });
  });
}
