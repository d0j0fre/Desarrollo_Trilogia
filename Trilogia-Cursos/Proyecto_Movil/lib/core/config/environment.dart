import 'package:flutter/foundation.dart';

/// Ambiente de ejecucion. Se deriva del modo de compilacion en vez de una
/// bandera propia, para que nadie pueda publicar un APK de release con el
/// comportamiento relajado de desarrollo.
enum Environment { development, production }

Environment get currentEnvironment =>
    kReleaseMode ? Environment.production : Environment.development;

bool get isProduction => currentEnvironment == Environment.production;
