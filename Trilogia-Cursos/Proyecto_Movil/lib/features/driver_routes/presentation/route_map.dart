import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../domain/delivery.dart';

/// Paradas con ubicacion, en el orden de visita que definio logistica
/// (la secuencia de la ruta).
List<Delivery> stopsForMap(List<Delivery> deliveries) =>
    deliveries.where((d) => d.tieneCoordenadas).toList()..sort((a, b) => a.secuencia.compareTo(b.secuencia));

/// Mapa interactivo de la ruta (BUG-25-04 · CU-252): cada parada con su numero
/// de visita y la linea del recorrido en ese orden. Se puede mover y acercar.
///
/// Las teselas son de OpenStreetMap y necesitan señal; sin ella el mapa queda
/// en blanco pero la lista de paradas sigue funcionando debajo.
class RouteMap extends StatelessWidget {
  const RouteMap({super.key, required this.deliveries, this.height = 260, this.onExpand});

  final List<Delivery> deliveries;
  final double height;
  final VoidCallback? onExpand;

  @override
  Widget build(BuildContext context) {
    final stops = stopsForMap(deliveries);
    final withoutLocation = deliveries.length - stops.length;
    final theme = Theme.of(context);

    if (stops.isEmpty) {
      return Card(
        child: ListTile(
          leading: const Icon(Icons.map_outlined, color: BrandColors.textMuted),
          title: const Text('Mapa de la ruta'),
          subtitle: const Text('Ninguna parada tiene ubicación registrada. Seguí la secuencia de la lista.'),
        ),
      );
    }

    final points = [for (final stop in stops) LatLng(stop.latitud!, stop.longitud!)];

    return Card(
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            height: height,
            child: Stack(
              children: [
                FlutterMap(
                  options: MapOptions(
                    initialCenter: points.first,
                    initialZoom: 15,
                    initialCameraFit: points.length > 1
                        ? CameraFit.coordinates(coordinates: points, padding: const EdgeInsets.all(36))
                        : null,
                    interactionOptions: const InteractionOptions(flags: InteractiveFlag.all & ~InteractiveFlag.rotate),
                  ),
                  children: [
                    TileLayer(
                      urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                      userAgentPackageName: 'cr.distribuidorajj.proyecto_movil',
                    ),
                    if (points.length > 1)
                      PolylineLayer(polylines: [
                        Polyline(points: points, strokeWidth: 4, color: BrandColors.redPrimary.withValues(alpha: 0.7)),
                      ]),
                    MarkerLayer(markers: [
                      for (var i = 0; i < stops.length; i++)
                        Marker(
                          point: points[i],
                          width: 34,
                          height: 34,
                          // El mismo numero que la tarjeta de la parada en la lista.
                          child: _StopMarker(number: stops[i].secuencia > 0 ? stops[i].secuencia : i + 1, delivery: stops[i]),
                        ),
                    ]),
                    const SimpleAttributionWidget(source: Text('OpenStreetMap')),
                  ],
                ),
                if (onExpand != null)
                  Positioned(
                    top: 8,
                    right: 8,
                    child: IconButton.filledTonal(
                      onPressed: onExpand,
                      tooltip: 'Ver mapa completo',
                      icon: const Icon(Icons.fullscreen_rounded),
                    ),
                  ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(14, 10, 14, 12),
            child: Text(
              withoutLocation == 0
                  ? 'Secuencia de visitas: ${stops.length} ${stops.length == 1 ? "parada" : "paradas"} en el orden de la ruta.'
                  : 'Secuencia de visitas: ${stops.length} en el mapa · $withoutLocation sin ubicación (ver lista).',
              style: theme.textTheme.bodySmall,
            ),
          ),
        ],
      ),
    );
  }
}

class _StopMarker extends StatelessWidget {
  const _StopMarker({required this.number, required this.delivery});

  final int number;
  final Delivery delivery;

  @override
  Widget build(BuildContext context) {
    final color = delivery.estaCerrada ? AppTheme.deliveryStatusColor(delivery.estadoEntrega) : BrandColors.redPrimary;
    return Tooltip(
      message: '$number. ${delivery.cliente}',
      child: Container(
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: color,
          shape: BoxShape.circle,
          border: Border.all(color: BrandColors.white, width: 2.5),
          boxShadow: const [BoxShadow(blurRadius: 4, color: Color(0x55000000))],
        ),
        child: Text('$number', style: const TextStyle(color: BrandColors.white, fontWeight: FontWeight.w800)),
      ),
    );
  }
}

/// El mapa a pantalla completa.
class RouteMapPage extends StatelessWidget {
  const RouteMapPage({super.key, required this.title, required this.deliveries});

  final String title;
  final List<Delivery> deliveries;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: LayoutBuilder(
        builder: (context, constraints) => SingleChildScrollView(
          padding: const EdgeInsets.all(8),
          child: RouteMap(deliveries: deliveries, height: constraints.maxHeight - 80),
        ),
      ),
    );
  }
}
