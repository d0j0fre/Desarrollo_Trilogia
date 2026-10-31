import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/features/driver_routes/domain/delivery.dart';
import 'package:proyecto_movil/features/driver_routes/presentation/route_map.dart';

/// BUG-25-04: el mapa de la ruta muestra las paradas en la secuencia de visita
/// que definió logística.
void main() {
  Delivery parada(int secuencia, {double? lat, double? lng}) => Delivery(
        rutaPedidoId: secuencia,
        pedidoId: 100 + secuencia,
        secuencia: secuencia,
        estadoEntrega: DeliveryStatus.enRuta,
        cliente: 'Cliente $secuencia',
        telefono: '',
        direccion: 'San José',
        total: 1000,
        latitud: lat,
        longitud: lng,
      );

  test('las paradas con ubicación salen en el orden de la secuencia', () {
    final stops = stopsForMap([
      parada(3, lat: 9.93, lng: -84.06),
      parada(1, lat: 9.93, lng: -84.08),
      parada(2, lat: 9.94, lng: -84.07),
    ]);

    expect(stops.map((s) => s.secuencia), [1, 2, 3]);
  });

  test('una parada sin ubicación no entra al mapa pero no altera el orden', () {
    final stops = stopsForMap([
      parada(2),
      parada(1, lat: 9.93, lng: -84.08),
      parada(3, lat: 0, lng: 0),
      parada(4, lat: 9.94, lng: -84.07),
    ]);

    expect(stops.map((s) => s.secuencia), [1, 4]);
  });
}
