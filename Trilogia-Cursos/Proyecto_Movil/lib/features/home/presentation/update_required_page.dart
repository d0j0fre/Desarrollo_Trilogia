import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../shared/utils/navigation_launcher.dart';
import '../domain/day_summary.dart';

/// Resultado de la compuerta de version.
enum VersionVerdict { permitida, sugiereActualizar, bloqueada }

class VersionCheck {
  const VersionCheck(this.verdict, {this.gate});

  final VersionVerdict verdict;
  final AppVersionGate? gate;
}

/// Consulta la version minima soportada al arrancar.
///
/// Sin tienda de aplicaciones no hay actualizacion automatica: esta es la unica
/// forma de retirar del campo una compilacion con un error grave.
///
/// Si la consulta falla, **deja pasar**. Dejar a toda la flota fuera de la
/// aplicacion porque el servidor no respondio seria peor que el problema que
/// esta compuerta resuelve.
final versionCheckProvider = FutureProvider<VersionCheck>((ref) async {
  try {
    final info = await PackageInfo.fromPlatform();
    final buildInstalado = int.tryParse(info.buildNumber) ?? 0;

    final data = await ref
        .read(apiClientProvider)
        .get<Map<String, dynamic>>(
          'api/mobile/v1/app/version',
          anonymous: true,
        );

    final gate = AppVersionGate.fromJson(data);

    if (gate.bloquea(buildInstalado)) {
      return VersionCheck(VersionVerdict.bloqueada, gate: gate);
    }
    if (gate.sugiereActualizar(buildInstalado)) {
      return VersionCheck(VersionVerdict.sugiereActualizar, gate: gate);
    }
    return const VersionCheck(VersionVerdict.permitida);
  } on ApiException {
    return const VersionCheck(VersionVerdict.permitida);
  } catch (_) {
    return const VersionCheck(VersionVerdict.permitida);
  }
});

/// Pantalla bloqueante. Sin salida a proposito: la version instalada ya no
/// puede operar contra este servidor.
class UpdateRequiredPage extends ConsumerWidget {
  const UpdateRequiredPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final check = ref.watch(versionCheckProvider).value;
    final gate = check?.gate;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: AppTheme.overlayOnBrand,
      child: Scaffold(
        body: Container(
          width: double.infinity,
          decoration: const BoxDecoration(gradient: BrandColors.headerGradient),
          child: SafeArea(
            child: Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(28),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 440),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const Center(child: BrandLogo(size: 104)),
                      const SizedBox(height: 28),
                      const Text(
                        'Necesitás actualizar',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          color: BrandColors.white,
                          fontSize: 26,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 12),
                      Text(
                        gate?.mandatoryMessage ??
                            'Esta versión de la aplicación ya no se puede usar. '
                                'Descargá la nueva para seguir trabajando.',
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          color: BrandColors.white,
                          fontSize: 16,
                          height: 1.45,
                        ),
                      ),
                      if (gate != null && gate.latestVersion.isNotEmpty) ...[
                        const SizedBox(height: 14),
                        Center(
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 12,
                              vertical: 5,
                            ),
                            decoration: BoxDecoration(
                              color: BrandColors.white.withValues(alpha: 0.12),
                              borderRadius: BorderRadius.circular(999),
                            ),
                            child: Text(
                              'Versión disponible: ${gate.latestVersion}',
                              style: const TextStyle(
                                color: BrandColors.goldLight,
                                fontSize: 14,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ),
                        ),
                      ],
                      const SizedBox(height: 32),
                      if (gate != null && gate.downloadUrl.isNotEmpty)
                        ElevatedButton.icon(
                          style: ElevatedButton.styleFrom(
                            backgroundColor: BrandColors.white,
                            foregroundColor: BrandColors.wine,
                          ),
                          icon: const Icon(Icons.download_rounded),
                          label: const Text('Descargar actualización'),
                          onPressed: () =>
                              NavigationLauncher.abrirEnlace(gate.downloadUrl),
                        )
                      else
                        const Text(
                          'Pedile el enlace de descarga a administración.',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            color: BrandColors.goldLight,
                            fontSize: 15,
                          ),
                        ),
                      const SizedBox(height: 12),
                      TextButton(
                        style: TextButton.styleFrom(
                          foregroundColor: BrandColors.goldLight,
                        ),
                        onPressed: () => ref.invalidate(versionCheckProvider),
                        child: const Text('Ya actualicé, volver a revisar'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
