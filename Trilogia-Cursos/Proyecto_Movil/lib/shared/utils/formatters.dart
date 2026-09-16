import 'package:intl/intl.dart';

/// Formatos de la aplicación. En un solo lugar para que un monto o una fecha se
/// vean igual en todas las pantallas.
class Fmt {
  const Fmt._();

  static final NumberFormat _money = NumberFormat.currency(
    locale: 'es_CR',
    symbol: '₡',
    decimalDigits: 0,
    // En Costa Rica el símbolo va antes del monto: ₡184.500.
    customPattern: '¤#,##0',
  );

  static final NumberFormat _number = NumberFormat.decimalPattern('es_CR');

  static String money(num value) => _money.format(value);

  /// ₡1,2 M en vez de ₡1.234.567 donde el espacio es poco (tarjetas de métricas).
  static String moneyCompact(num value) {
    final abs = value.abs();
    if (abs < 100000) return _money.format(value);
    final sign = value < 0 ? '-' : '';
    // Armado a mano: el formato compacto de intl pone el símbolo al final.
    if (abs < 1000000) return '$sign₡${_number.format((abs / 1000).round())} mil';
    return '$sign₡${NumberFormat('#,##0.#', 'es_CR').format(abs / 1000000)} M';
  }

  static String number(num value) => _number.format(value);

  static String date(DateTime? value) =>
      value == null ? '—' : DateFormat('dd/MM/yyyy').format(value);

  static String dateTime(DateTime? value) =>
      value == null ? '—' : DateFormat('dd/MM/yyyy · HH:mm').format(value);

  static String shortDateTime(DateTime? value) =>
      value == null ? '—' : DateFormat('dd/MM · HH:mm').format(value);

  static String hours(num value) {
    final text = value == value.roundToDouble()
        ? value.toInt().toString()
        : value.toStringAsFixed(1);
    return '$text h';
  }

  /// Nombre legible de los estados que la base guarda sin espacios.
  static String status(String value) => switch (value) {
        'EnProceso' => 'En proceso',
        'EnRuta' => 'En ruta',
        'RecibidaParcial' => 'Recibida parcial',
        'CerradaConDiscrepancia' => 'Con discrepancia',
        'Fallido' => 'No entregado',
        _ => value,
      };
}

/// Lectura tolerante de JSON: un campo ausente o nulo no tumba la pantalla.
class J {
  const J._();

  static int integer(Object? value) =>
      value is num ? value.toInt() : int.tryParse('$value') ?? 0;

  static double decimal(Object? value) =>
      value is num ? value.toDouble() : double.tryParse('$value') ?? 0;

  static String text(Object? value) => value == null ? '' : '$value';

  static bool boolean(Object? value) => value == true;

  static DateTime? date(Object? value) =>
      value == null ? null : DateTime.tryParse('$value');

  static List<T> list<T>(Object? value, T Function(Map<String, dynamic>) map) =>
      (value as List<dynamic>? ?? const [])
          .map((item) => map(item as Map<String, dynamic>))
          .toList();
}
