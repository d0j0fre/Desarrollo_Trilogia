import 'package:url_launcher/url_launcher.dart';

/// Abre la navegacion hacia una parada.
///
/// El orden de intentos importa: Waze primero porque es lo que la flota ya usa
/// y lo que la vista web del chofer abre hoy
/// (`Views/DriverDeliveries/Route.cshtml`). Si no esta instalado, Google Maps.
/// Si tampoco, se abre Waze en el navegador, que ofrece instalarlo.
///
/// La URL es exactamente la misma del sistema web. Vale la pena que sea la
/// misma: si un dia cambia el formato, cambia en los dos lados o en ninguno.
class NavigationLauncher {
  const NavigationLauncher._();

  static Future<bool> abrirRuta({
    required double latitud,
    required double longitud,
  }) async {
    final candidatos = <Uri>[
      Uri.parse('waze://?ll=$latitud,$longitud&navigate=yes'),
      Uri.parse('google.navigation:q=$latitud,$longitud'),
      Uri.parse('https://waze.com/ul?ll=$latitud%2C$longitud&navigate=yes'),
      Uri.parse(
        'https://www.google.com/maps/dir/?api=1&destination=$latitud,$longitud',
      ),
    ];

    for (final uri in candidatos) {
      try {
        if (await canLaunchUrl(uri)) {
          final abierto = await launchUrl(
            uri,
            mode: LaunchMode.externalApplication,
          );
          if (abierto) return true;
        }
      } catch (_) {
        // Se prueba el siguiente. Que un esquema no este registrado en el
        // telefono es normal, no un error que valga la pena mostrar.
      }
    }

    return false;
  }

  /// Busca una direccion escrita cuando el cliente no tiene coordenadas. Antes
  /// el boton de navegar simplemente no aparecia y el chofer tenia que copiar
  /// la direccion a mano.
  static Future<bool> buscarDireccion(String direccion) async {
    final texto = direccion.trim();
    if (texto.isEmpty) return false;

    final consulta = Uri.encodeComponent(texto);
    final candidatos = <Uri>[
      Uri.parse('waze://?q=$consulta&navigate=yes'),
      Uri.parse('geo:0,0?q=$consulta'),
      Uri.parse('https://www.google.com/maps/search/?api=1&query=$consulta'),
    ];

    for (final uri in candidatos) {
      try {
        if (await canLaunchUrl(uri)) {
          final abierto = await launchUrl(
            uri,
            mode: LaunchMode.externalApplication,
          );
          if (abierto) return true;
        }
      } catch (_) {
        // Se prueba el siguiente.
      }
    }

    return false;
  }

  /// Llama al cliente. Marca el numero pero no inicia la llamada: el sistema
  /// abre el marcador con el numero puesto y la persona decide.
  static Future<bool> llamar(String telefono) async {
    final limpio = telefono.replaceAll(RegExp(r'[^0-9+]'), '');
    if (limpio.isEmpty) return false;

    final uri = Uri.parse('tel:$limpio');
    try {
      if (await canLaunchUrl(uri)) {
        return launchUrl(uri);
      }
    } catch (_) {
      return false;
    }
    return false;
  }

  static Future<bool> abrirEnlace(String url) async {
    final uri = Uri.tryParse(url);
    if (uri == null || !uri.isScheme('https')) return false;

    try {
      return await launchUrl(uri, mode: LaunchMode.externalApplication);
    } catch (_) {
      return false;
    }
  }
}
