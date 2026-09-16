import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'brand_colors.dart';

/// Tema de la aplicacion.
///
/// Tres decisiones que vienen de como se usa esto en la calle, no de como se
/// ve en una computadora:
///
/// - **48 dp minimo de area tactil** (52 en botones principales). El chofer
///   toca la pantalla de pie, en movimiento y a veces con guantes.
/// - **16 sp de texto base.** Hay que poder leerlo al sol.
/// - **Sin tema oscuro.** Una sola apariencia, de alto contraste, que se
///   comporta igual a las 6 de la mañana y a las 3 de la tarde.
///
/// Las pantallas no escriben colores ni radios sueltos: salen de aqui, para
/// que todo se vea de la misma familia.
class AppTheme {
  const AppTheme._();

  static const double minTouchTarget = 48;
  static const double buttonHeight = 52;
  static const double radius = 16;
  static const double radiusSmall = 12;

  /// Barra de estado clara sobre los encabezados vino.
  static const SystemUiOverlayStyle overlayOnBrand = SystemUiOverlayStyle(
    statusBarColor: Colors.transparent,
    statusBarIconBrightness: Brightness.light,
    statusBarBrightness: Brightness.dark,
  );

  /// Barra de estado oscura sobre las pantallas crema.
  static const SystemUiOverlayStyle overlayOnSurface = SystemUiOverlayStyle(
    statusBarColor: Colors.transparent,
    statusBarIconBrightness: Brightness.dark,
    statusBarBrightness: Brightness.light,
  );

  static ThemeData build() {
    final scheme =
        ColorScheme.fromSeed(
          seedColor: BrandColors.redPrimary,
          brightness: Brightness.light,
        ).copyWith(
          primary: BrandColors.redPrimary,
          onPrimary: BrandColors.white,
          primaryContainer: const Color(0xFFF6E3E1),
          onPrimaryContainer: BrandColors.wine,
          secondary: BrandColors.goldPrimary,
          onSecondary: BrandColors.black,
          surface: BrandColors.white,
          onSurface: BrandColors.black,
          onSurfaceVariant: BrandColors.textMuted,
          surfaceContainerLowest: BrandColors.white,
          surfaceContainerLow: BrandColors.surface,
          surfaceContainer: BrandColors.surface,
          surfaceContainerHigh: BrandColors.surfaceAlt,
          outline: BrandColors.border,
          outlineVariant: BrandColors.border,
          error: BrandColors.danger,
        );

    final rounded = RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(radiusSmall),
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: BrandColors.surface,
      visualDensity: VisualDensity.standard,
      splashFactory: InkSparkle.splashFactory,

      appBarTheme: const AppBarTheme(
        backgroundColor: BrandColors.surface,
        foregroundColor: BrandColors.black,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0.5,
        shadowColor: BrandColors.border,
        centerTitle: false,
        systemOverlayStyle: overlayOnSurface,
        titleTextStyle: TextStyle(
          color: BrandColors.black,
          fontSize: 20,
          fontWeight: FontWeight.w700,
          letterSpacing: -0.2,
        ),
      ),

      textTheme: const TextTheme(
        headlineMedium: TextStyle(
          fontSize: 26,
          fontWeight: FontWeight.w800,
          letterSpacing: -0.5,
          color: BrandColors.black,
        ),
        headlineSmall: TextStyle(
          fontSize: 22,
          fontWeight: FontWeight.w700,
          letterSpacing: -0.3,
          color: BrandColors.black,
        ),
        titleLarge: TextStyle(
          fontSize: 19,
          fontWeight: FontWeight.w700,
          color: BrandColors.black,
        ),
        titleMedium: TextStyle(
          fontSize: 17,
          fontWeight: FontWeight.w600,
          color: BrandColors.black,
        ),
        titleSmall: TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w700,
          letterSpacing: 0.6,
          color: BrandColors.textMuted,
        ),
        bodyLarge: TextStyle(
          fontSize: 16,
          height: 1.4,
          color: BrandColors.black,
        ),
        bodyMedium: TextStyle(
          fontSize: 15,
          height: 1.35,
          color: BrandColors.black,
        ),
        bodySmall: TextStyle(
          fontSize: 13,
          height: 1.35,
          color: BrandColors.textMuted,
        ),
        labelLarge: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
      ),

      cardTheme: CardThemeData(
        color: BrandColors.white,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(radius),
          side: const BorderSide(color: BrandColors.border),
        ),
      ),

      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: BrandColors.redPrimary,
          foregroundColor: BrandColors.white,
          disabledBackgroundColor: BrandColors.surfaceAlt,
          disabledForegroundColor: BrandColors.textMuted,
          elevation: 0,
          minimumSize: const Size.fromHeight(buttonHeight),
          padding: const EdgeInsets.symmetric(horizontal: 16),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
          shape: rounded,
        ),
      ),

      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: BrandColors.redPrimary,
          foregroundColor: BrandColors.white,
          minimumSize: const Size.fromHeight(buttonHeight),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
          shape: rounded,
        ),
      ),

      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: BrandColors.view,
          backgroundColor: BrandColors.white,
          minimumSize: const Size.fromHeight(minTouchTarget),
          padding: const EdgeInsets.symmetric(horizontal: 12),
          side: const BorderSide(color: BrandColors.border, width: 1.5),
          textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
          shape: rounded,
        ),
      ),

      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: BrandColors.view,
          minimumSize: const Size(minTouchTarget, minTouchTarget),
          textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
          shape: rounded,
        ),
      ),

      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(
          minimumSize: const Size(minTouchTarget, minTouchTarget),
        ),
      ),

      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: BrandColors.surface,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 18,
        ),
        prefixIconColor: BrandColors.textMuted,
        suffixIconColor: BrandColors.textMuted,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radiusSmall),
          borderSide: const BorderSide(color: BrandColors.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radiusSmall),
          borderSide: const BorderSide(color: BrandColors.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radiusSmall),
          borderSide: const BorderSide(color: BrandColors.redPrimary, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radiusSmall),
          borderSide: const BorderSide(color: BrandColors.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radiusSmall),
          borderSide: const BorderSide(color: BrandColors.danger, width: 2),
        ),
        labelStyle: const TextStyle(fontSize: 15, color: BrandColors.textMuted),
        floatingLabelStyle: const TextStyle(
          fontSize: 15,
          color: BrandColors.redPrimary,
          fontWeight: FontWeight.w600,
        ),
        errorStyle: const TextStyle(fontSize: 13, color: BrandColors.danger),
      ),

      progressIndicatorTheme: const ProgressIndicatorThemeData(
        color: BrandColors.redPrimary,
        linearTrackColor: BrandColors.surfaceAlt,
      ),

      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: BrandColors.black,
        contentTextStyle: const TextStyle(
          fontSize: 15,
          color: BrandColors.white,
        ),
        shape: rounded,
      ),

      bottomSheetTheme: const BottomSheetThemeData(
        backgroundColor: BrandColors.white,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        dragHandleColor: BrandColors.border,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
      ),

      dividerTheme: const DividerThemeData(
        color: BrandColors.border,
        space: 1,
        thickness: 1,
      ),

      radioTheme: RadioThemeData(
        fillColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? BrandColors.redPrimary
              : BrandColors.textMuted,
        ),
      ),

      listTileTheme: const ListTileThemeData(
        minVerticalPadding: 12,
        iconColor: BrandColors.textMuted,
        contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      ),

      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
        },
      ),
    );
  }

  /// Color con el que se pinta cada estado de entrega. Vive aqui y no en cada
  /// pantalla para que la lista y el detalle no se puedan contradecir.
  static Color deliveryStatusColor(String status) => switch (status) {
    'Entregado' => BrandColors.delivered,
    'Fallido' => BrandColors.failed,
    'EnRuta' => BrandColors.inRoute,
    _ => BrandColors.pending,
  };

  static String deliveryStatusLabel(String status) => switch (status) {
    'Entregado' => 'Entregado',
    'Fallido' => 'No entregado',
    'EnRuta' => 'En ruta',
    _ => 'Pendiente',
  };

  static IconData deliveryStatusIcon(String status) => switch (status) {
    'Entregado' => Icons.check_circle_rounded,
    'Fallido' => Icons.cancel_rounded,
    'EnRuta' => Icons.local_shipping_rounded,
    _ => Icons.schedule_rounded,
  };
}
