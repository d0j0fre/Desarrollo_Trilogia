/// Nombre legible de un estado interno del sistema.
///
/// El servidor guarda los estados en PascalCase («EnRuta», «RecibidaParcial»).
/// Esa forma nunca se muestra al usuario: se pasa siempre por aquí. El valor
/// crudo es el que viaja a la API y el que usa la lógica; esto solo se pinta.
String statusLabel(String raw) {
  final value = raw.trim();
  if (value.isEmpty) return value;
  final known = _knownLabels[value];
  if (known != null) return known;
  // Estado desconocido: «PagoParcial» → «Pago parcial».
  final spaced = value.replaceAllMapped(
    RegExp(r'(?<=[a-záéíóúñ])(?=[A-ZÁÉÍÓÚÑ])'),
    (_) => ' ',
  );
  if (spaced == value) return value;
  final lower = spaced.toLowerCase();
  return lower[0].toUpperCase() + lower.substring(1);
}

const Map<String, String> _knownLabels = {
  'EnProceso': 'En proceso',
  'EnRuta': 'En ruta',
  'RecibidaParcial': 'Recibida parcial',
  'CerradaConDiscrepancia': 'Cerrada con discrepancia',
  'Fallido': 'No entregado',
  'ConDeuda': 'Con deuda',
  'SinCredito': 'Sin crédito',
};
