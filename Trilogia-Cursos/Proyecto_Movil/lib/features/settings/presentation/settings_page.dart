import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../../../core/config/app_config.dart';
import '../../../core/providers.dart';
import '../../../core/sync/sync_service.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../auth/application/auth_controller.dart';

final packageInfoProvider = FutureProvider<PackageInfo>(
  (ref) => PackageInfo.fromPlatform(),
);

class SettingsPage extends ConsumerWidget {
  const SettingsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionProvider);
    final info = ref.watch(packageInfoProvider);
    final sync = ref.watch(syncStateProvider).value ?? const SyncState();
    final theme = Theme.of(context);

    final iniciales = (session?.fullName ?? '')
        .trim()
        .split(RegExp(r'\s+'))
        .where((parte) => parte.isNotEmpty)
        .take(2)
        .map((parte) => parte[0].toUpperCase())
        .join();

    return Scaffold(
      appBar: AppBar(title: const Text('Mi cuenta')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
        children: [
          if (session != null) ...[
            Card(
              child: Padding(
                padding: const EdgeInsets.all(18),
                child: Row(
                  children: [
                    Container(
                      width: 60,
                      height: 60,
                      alignment: Alignment.center,
                      decoration: const BoxDecoration(
                        gradient: BrandColors.headerGradient,
                        shape: BoxShape.circle,
                      ),
                      child: Text(
                        iniciales.isEmpty ? '?' : iniciales,
                        style: const TextStyle(
                          color: BrandColors.white,
                          fontSize: 22,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            session.fullName,
                            style: theme.textTheme.titleLarge,
                          ),
                          const SizedBox(height: 2),
                          Text(
                            session.email,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: theme.textTheme.bodySmall,
                          ),
                          if (session.role.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            StatusChip(
                              label: session.role,
                              color: BrandColors.redPrimary,
                              icon: Icons.badge_outlined,
                            ),
                          ],
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 24),
          ],
          const SectionTitle('Sincronización'),
          Card(
            child: ListTile(
              leading: IconBadge(
                icon: sync.conflictos > 0
                    ? Icons.error_outline_rounded
                    : sync.pendientes > 0
                    ? Icons.cloud_upload_outlined
                    : Icons.cloud_done_outlined,
                color: sync.conflictos > 0
                    ? BrandColors.danger
                    : sync.pendientes > 0
                    ? BrandColors.pending
                    : BrandColors.delivered,
              ),
              title: const Text('Por enviar'),
              subtitle: Text(
                sync.conflictos > 0
                    ? '${sync.conflictos} ${sync.conflictos == 1 ? "acción necesita" : "acciones necesitan"} atención'
                    : sync.pendientes > 0
                    ? '${sync.pendientes} ${sync.pendientes == 1 ? "acción pendiente" : "acciones pendientes"}'
                    : 'Todo al día',
              ),
              trailing: const Icon(Icons.chevron_right_rounded),
              onTap: () => context.push('/pendientes'),
            ),
          ),
          const SizedBox(height: 24),
          const SectionTitle('Aplicación'),
          Card(
            child: Column(
              children: [
                ListTile(
                  leading: const IconBadge(
                    icon: Icons.info_outline_rounded,
                    color: BrandColors.inRoute,
                  ),
                  title: const Text('Versión'),
                  subtitle: Text(
                    info.when(
                      loading: () => '…',
                      error: (_, _) => 'Desconocida',
                      data: (data) =>
                          '${data.version} (compilación ${data.buildNumber})',
                    ),
                  ),
                ),
                const Divider(indent: 72),
                const ListTile(
                  leading: IconBadge(
                    icon: Icons.support_agent_rounded,
                    color: BrandColors.pending,
                  ),
                  title: Text('¿Algo no funciona?'),
                  subtitle: Text(
                    'Avisale a operaciones. Si marcaste entregas sin señal, '
                    'no desinstales la aplicación: se perderían.',
                  ),
                  isThreeLine: true,
                ),
              ],
            ),
          ),
          const SizedBox(height: 28),
          OutlinedButton.icon(
            style: OutlinedButton.styleFrom(
              foregroundColor: BrandColors.danger,
              minimumSize: const Size.fromHeight(52),
              side: BorderSide(
                color: BrandColors.danger.withValues(alpha: 0.5),
                width: 1.5,
              ),
            ),
            icon: const Icon(Icons.logout_rounded),
            label: const Text('Cerrar sesión'),
            onPressed: () => _logout(context, ref),
          ),
          const SizedBox(height: 24),
          Center(
            child: Column(
              children: [
                const BrandLogo(size: 40, shadow: false),
                const SizedBox(height: 8),
                Text(AppConfig.companyName, style: theme.textTheme.bodySmall),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    // Se avisa de lo que se pierde. Cerrar sesion borra la cola a proposito:
    // si quedaran acciones sin enviar y entrara otro usuario, se enviarian con
    // su token y la bitacora registraria al chofer equivocado.
    final pendientes = ref.read(syncStateProvider).value?.pendientes ?? 0;

    final confirmado = await confirmSheet(
      context,
      title: 'Cerrar sesión',
      message: pendientes > 0
          ? 'Tenés $pendientes ${pendientes == 1 ? "acción que todavía no se envió" : "acciones que todavía no se enviaron"}. '
                'Si cerrás sesión se van a borrar del teléfono junto con tus rutas.'
          : 'Se van a borrar del teléfono tus rutas guardadas. Podés volver a '
                'entrar cuando quieras.',
      confirmLabel: 'Cerrar sesión',
      confirmColor: BrandColors.danger,
      icon: Icons.logout_rounded,
    );

    if (!confirmado) return;
    await ref.read(authControllerProvider.notifier).logout();
  }
}
