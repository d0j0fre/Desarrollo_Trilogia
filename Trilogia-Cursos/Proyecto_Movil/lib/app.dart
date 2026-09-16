import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/config/app_config.dart';
import 'core/providers.dart';
import 'core/routing/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/application/auth_controller.dart';

class OperacionApp extends ConsumerStatefulWidget {
  const OperacionApp({super.key});

  @override
  ConsumerState<OperacionApp> createState() => _OperacionAppState();
}

class _OperacionAppState extends ConsumerState<OperacionApp> {
  @override
  void initState() {
    super.initState();

    // Se arranca despues del primer cuadro para no bloquear la pantalla
    // inicial con trabajo de disco y de red.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(connectivityProvider);
      ref.read(authControllerProvider.notifier).restore();
    });
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: AppConfig.companyName,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.build(),
      routerConfig: ref.watch(routerProvider),
    );
  }
}
