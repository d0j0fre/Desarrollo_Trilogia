import 'package:flutter/material.dart';

/// Identidad visual de Distribuidora JJ.
///
/// La paleta sale del logo (vino, dorado cobre y crema) y conserva los colores
/// por intencion del sistema web (`design-system-buttons.css`): verde confirma,
/// rojo advierte. Asi el chofer reconoce "confirmar" y "cancelar" sin leer, y
/// la aplicacion se ve de la misma familia que el logo.
///
/// Los contrastes indicados son contra blanco salvo que se diga otra cosa, y
/// todos los textos cumplen WCAG AA (4.5:1) sobre su fondo previsto.
class BrandColors {
  const BrandColors._();

  // ── Marca (logo) ─────────────────────────────────────────────────────────
  static const Color redPrimary = Color(0xFF8B0E16); // 9.67:1
  static const Color redSecondary = Color(0xFFA3171E);
  static const Color wine = Color(0xFF6B0E14); // fondo de encabezados
  static const Color wineDeep = Color(0xFF3F070B); // extremo del degradado
  static const Color goldPrimary = Color(0xFFC98A2E); // decorativo, no texto
  static const Color goldLight = Color(0xFFF1D9A8); // 9.6:1 sobre `wine`
  static const Color cream = Color(0xFFF8F4F0);

  // ── Neutros ──────────────────────────────────────────────────────────────
  static const Color black = Color(0xFF1E1A18); // tinta principal
  static const Color white = Color(0xFFFFFFFF);
  static const Color surface = Color(0xFFF6F2EE); // fondo de pantallas
  static const Color surfaceAlt = Color(0xFFEFE8E2); // rellenos y rieles
  static const Color textMuted = Color(
    0xFF6B625D,
  ); // 5.8:1, 5.3:1 sobre surface
  static const Color border = Color(0xFFE6DED7);

  // ── Intencion de las acciones (sistema web) ──────────────────────────────
  static const Color action = Color(0xFF1F6F4A); // 6.12:1
  static const Color actionHover = Color(0xFF1A5C3D);
  static const Color view = Color(0xFF6E1622); // 11.66:1
  static const Color edit = Color(0xFFC9A227); // 6.81:1 con texto #1F1F1F
  static const Color editInk = Color(0xFF1F1F1F);
  static const Color danger = Color(0xFFB42318); // 6.4:1
  static const Color neutral = Color(0xFF262626);

  // ── Estados de entrega ───────────────────────────────────────────────────
  // Forma e icono los acompañan siempre: el color solo no basta para quien no
  // distingue rojo de verde.
  static const Color pending = Color(0xFF8A5A00); // 5.8:1
  static const Color inRoute = Color(0xFF1F5F8B); // 6.9:1
  static const Color delivered = action;
  static const Color failed = danger;

  static const Color offline = Color(0xFF8A5A00);

  /// Degradado de los encabezados (login, inicio, actualizacion).
  static const LinearGradient headerGradient = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [redPrimary, wine, wineDeep],
    stops: [0, 0.55, 1],
  );
}
