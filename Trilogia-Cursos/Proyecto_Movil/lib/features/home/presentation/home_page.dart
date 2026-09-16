import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/config/app_config.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../auth/application/auth_controller.dart';
import '../../auth/domain/session.dart';
import '../domain/day_summary.dart';

final daySummaryProvider = FutureProvider.autoDispose<DaySummary>(
  (ref) => ref.watch(routesRepositoryProvider).daySummary(),
);

/// Pantalla de inicio.
///
/// Las tarjetas salen de `/me/capabilities`, no de un `if` sobre el rol. El dia
/// que a un chofer le den ademas un permiso de bodega, su pantalla cambia sin
/// publicar un APK nuevo — que con distribucion por archivo es la diferencia
/// entre un cambio de minutos y uno de dias.
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final session = auth.session;

    if (session == null) return const SizedBox.shrink();

    // El resumen es del chofer. Para otros perfiles ni se pide: seria una
    // consulta a Azure que siempre responde 403.
    final muestraResumen =
        session.esChofer || session.moduloHabilitado('driver.routes');

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: AppTheme.overlayOnBrand,
      child: Scaffold(
        body: RefreshIndicator(
          color: BrandColors.redPrimary,
          onRefresh: () async {
            if (muestraResumen) ref.invalidate(daySummaryProvider);
            await ref
                .read(authControllerProvider.notifier)
                .reintentarCapacidades();
          },
          child: ListView(
            padding: EdgeInsets.zero,
            physics: const AlwaysScrollableScrollPhysics(),
            children: [
              _HeaderWithSummary(
                session: session,
                muestraResumen: muestraResumen,
              ),
              const SyncBanner(),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 24, 16, 32),
                child: _Modules(auth: auth),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _HeaderWithSummary extends StatelessWidget {
  const _HeaderWithSummary({
    required this.session,
    required this.muestraResumen,
  });

  final Session session;
  final bool muestraResumen;

  static const _alturaResumen = 108.0;

  @override
  Widget build(BuildContext context) {
    final header = _Header(
      session: session,
      espacioInferior: muestraResumen ? _alturaResumen / 2 + 24 : 28,
    );

    if (!muestraResumen) return header;

    return Stack(
      children: [
        Column(
          children: [
            header,
            const SizedBox(height: _alturaResumen / 2),
          ],
        ),
        const Positioned(
          left: 16,
          right: 16,
          bottom: 0,
          height: _alturaResumen,
          child: _SummaryCard(),
        ),
      ],
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.session, required this.espacioInferior});

  final Session session;
  final double espacioInferior;

  @override
  Widget build(BuildContext context) {
    final hora = DateTime.now().hour;
    final saludo = hora < 12
        ? 'Buenos días'
        : hora < 19
        ? 'Buenas tardes'
        : 'Buenas noches';

    // Solo el primer nombre: en una pantalla de telefono, el nombre completo
    // de una persona empuja todo lo demas fuera de la linea.
    final primerNombre = session.fullName.trim().split(' ').first;

    return Container(
      decoration: const BoxDecoration(
        gradient: BrandColors.headerGradient,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
      ),
      padding: EdgeInsets.fromLTRB(
        20,
        MediaQuery.paddingOf(context).top + 12,
        8,
        espacioInferior,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const BrandLogo(size: 46, shadow: false),
              const SizedBox(width: 12),
              const Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      AppConfig.companyName,
                      style: TextStyle(
                        color: BrandColors.white,
                        fontSize: 17,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    Text(
                      AppConfig.appTagline,
                      style: TextStyle(
                        color: BrandColors.goldLight,
                        fontSize: 13,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ),
              ),
              IconButton(
                icon: const Icon(Icons.person_outline_rounded),
                color: BrandColors.white,
                tooltip: 'Mi cuenta y ajustes',
                onPressed: () => context.push('/ajustes'),
              ),
            ],
          ),
          const SizedBox(height: 26),
          Padding(
            padding: const EdgeInsets.only(right: 12),
            child: Text(
              primerNombre.isEmpty ? saludo : '$saludo,\n$primerNombre',
              style: const TextStyle(
                color: BrandColors.white,
                fontSize: 28,
                height: 1.15,
                fontWeight: FontWeight.w800,
                letterSpacing: -0.5,
              ),
            ),
          ),
          if (session.role.isNotEmpty) ...[
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                color: BrandColors.goldPrimary.withValues(alpha: 0.22),
                borderRadius: BorderRadius.circular(999),
                border: Border.all(
                  color: BrandColors.goldLight.withValues(alpha: 0.4),
                ),
              ),
              child: Text(
                session.role,
                style: const TextStyle(
                  color: BrandColors.goldLight,
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _SummaryCard extends ConsumerWidget {
  const _SummaryCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summary = ref.watch(daySummaryProvider);

    return Container(
      decoration: BoxDecoration(
        color: BrandColors.white,
        borderRadius: BorderRadius.circular(AppTheme.radius),
        boxShadow: [
          BoxShadow(
            color: BrandColors.wineDeep.withValues(alpha: 0.12),
            blurRadius: 24,
            offset: const Offset(0, 10),
          ),
        ],
      ),
      child: summary.when(
        loading: () => const _SummaryRow(),
        error: (_, _) => InkWell(
          borderRadius: BorderRadius.circular(AppTheme.radius),
          onTap: () => ref.invalidate(daySummaryProvider),
          child: const Padding(
            padding: EdgeInsets.symmetric(horizontal: 18),
            child: Row(
              children: [
                IconBadge(
                  icon: Icons.cloud_off_rounded,
                  color: BrandColors.offline,
                ),
                SizedBox(width: 14),
                Expanded(
                  child: Text(
                    'No se pudo traer el resumen del día. Tocá para reintentar.',
                    style: TextStyle(
                      fontSize: 14,
                      color: BrandColors.textMuted,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        data: (data) => _SummaryRow(summary: data),
      ),
    );
  }
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({this.summary});

  /// `null` mientras carga: se dibuja la misma estructura sin numeros para que
  /// la tarjeta no salte al llegar los datos.
  final DaySummary? summary;

  @override
  Widget build(BuildContext context) {
    Widget divisor() => const VerticalDivider(
      width: 1,
      indent: 22,
      endIndent: 22,
      color: BrandColors.border,
    );

    return Row(
      children: [
        Expanded(
          child: _Stat(
            valor: summary?.entregasPendientes,
            etiqueta: 'Pendientes',
            color: BrandColors.pending,
            icon: Icons.schedule_rounded,
          ),
        ),
        divisor(),
        Expanded(
          child: _Stat(
            valor: summary?.entregasCompletadasHoy,
            etiqueta: 'Entregadas',
            color: BrandColors.delivered,
            icon: Icons.check_circle_outline_rounded,
          ),
        ),
        divisor(),
        Expanded(
          child: _Stat(
            valor: summary?.entregasFallidasHoy,
            etiqueta: 'No entregadas',
            color: BrandColors.failed,
            icon: Icons.highlight_off_rounded,
          ),
        ),
      ],
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({
    required this.valor,
    required this.etiqueta,
    required this.color,
    required this.icon,
  });

  final int? valor;
  final String etiqueta;
  final Color color;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: '$etiqueta hoy: ${valor ?? "cargando"}',
      excludeSemantics: true,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 18, color: color),
              const SizedBox(width: 6),
              valor == null
                  ? Container(
                      width: 22,
                      height: 22,
                      decoration: BoxDecoration(
                        color: BrandColors.surfaceAlt,
                        borderRadius: BorderRadius.circular(6),
                      ),
                    )
                  : Text(
                      '$valor',
                      style: TextStyle(
                        fontSize: 26,
                        height: 1,
                        fontWeight: FontWeight.w800,
                        color: color,
                      ),
                    ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            etiqueta,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    );
  }
}

class _Modules extends ConsumerWidget {
  const _Modules({required this.auth});

  final AuthState auth;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = auth.session!;
    final habilitados = session.modulos
        .where((modulo) => modulo.enabled)
        .toList();

    if (habilitados.isNotEmpty) {
      // Agrupados por la sección que manda el servidor, en su mismo orden. Un
      // perfil con una sola sección no necesita títulos que la repitan.
      final agrupados = <String, List<AppModule>>{};
      for (final modulo in habilitados) {
        final seccion = modulo.section.isEmpty ? 'Mis funciones' : modulo.section;
        agrupados.putIfAbsent(seccion, () => []).add(modulo);
      }
      // Lo que se decide primero va arriba: quien gestiona ve la gestión antes
      // que el detalle de bodega u oficina.
      const orden = ['Operación en ruta', 'Gestión', 'Ventas', 'Bodega', 'Oficina', 'Personal'];
      final secciones = agrupados.entries.toList()
        ..sort((a, b) {
          final ia = orden.indexOf(a.key), ib = orden.indexOf(b.key);
          return (ia < 0 ? orden.length : ia).compareTo(ib < 0 ? orden.length : ib);
        });

      // Con muchos módulos, tarjetas compactas en dos columnas: la lista larga
      // obliga a desplazarse para encontrar lo que se usa a diario.
      final compacto = habilitados.length > 6;

      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final entry in secciones) ...[
            SectionTitle(secciones.length == 1 ? '¿Qué querés hacer?' : entry.key),
            if (compacto)
              _ModuleGrid(modulos: entry.value)
            else
              for (final modulo in entry.value)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _ModuleCard(modulo: modulo),
                ),
            const SizedBox(height: 10),
          ],
        ],
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SectionTitle('¿Qué querés hacer?'),
        if (!auth.capacidadesCargadas)
          // Todavia no se sabe que puede hacer esta persona: casi siempre es
          // falta de señal o el servidor despertando. Decir "sin permisos" aqui
          // seria falso y alarmante.
          _LoadingModules(
            onRetry: () => ref
                .read(authControllerProvider.notifier)
                .reintentarCapacidades(),
          )
        else
          const Card(
            child: EmptyState(
              icon: Icons.lock_outline_rounded,
              title: 'Sin funciones habilitadas',
              message:
                  'Tu perfil todavía no tiene acceso a ninguna función de la '
                  'aplicación. Hablá con administración.',
            ),
          ),
      ],
    );
  }
}

class _LoadingModules extends StatefulWidget {
  const _LoadingModules({required this.onRetry});

  final Future<void> Function() onRetry;

  @override
  State<_LoadingModules> createState() => _LoadingModulesState();
}

class _LoadingModulesState extends State<_LoadingModules> {
  bool _reintentando = false;

  Future<void> _retry() async {
    setState(() => _reintentando = true);
    await widget.onRetry();
    if (mounted) setState(() => _reintentando = false);
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          children: [
            const SizedBox(
              width: 28,
              height: 28,
              child: CircularProgressIndicator(strokeWidth: 3),
            ),
            const SizedBox(height: 16),
            Text(
              'Cargando tus funciones…',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 6),
            Text(
              'Si recién abrís la aplicación o tenés poca señal, puede tardar '
              'un momento.',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: _reintentando ? null : _retry,
              icon: const Icon(Icons.refresh_rounded, size: 20),
              label: Text(_reintentando ? 'Consultando…' : 'Reintentar'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ModuleGrid extends StatelessWidget {
  const _ModuleGrid({required this.modulos});

  final List<AppModule> modulos;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        for (var i = 0; i < modulos.length; i += 2)
          Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(child: _ModuleTile(modulo: modulos[i])),
                  const SizedBox(width: 10),
                  Expanded(
                    child: i + 1 < modulos.length
                        ? _ModuleTile(modulo: modulos[i + 1])
                        : const SizedBox(),
                  ),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

class _ModuleTile extends StatelessWidget {
  const _ModuleTile({required this.modulo});

  final AppModule modulo;

  @override
  Widget build(BuildContext context) {
    final destino = _ModuleCard._rutas[modulo.key];
    final color = destino == null ? BrandColors.textMuted : _ModuleCard._color(modulo.key);

    return Card(
      child: InkWell(
        onTap: destino == null ? null : () => context.push(destino),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              IconBadge(
                icon: _ModuleCard._icons[modulo.icon] ?? Icons.widgets_outlined,
                color: color,
                size: 44,
              ),
              const SizedBox(height: 12),
              Text(
                modulo.title,
                style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, height: 1.2),
              ),
              if (destino == null) ...[
                const SizedBox(height: 4),
                Text('Próximamente', style: Theme.of(context).textTheme.bodySmall),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ModuleCard extends StatelessWidget {
  const _ModuleCard({required this.modulo});

  final AppModule modulo;

  /// El icono llega del servidor como un nombre, no como un glifo: el servidor
  /// no deberia saber de iconografia de Material. Aqui se traduce.
  static const _icons = <String, IconData>{
    'route': Icons.route_rounded,
    'speedometer': Icons.speed_rounded,
    'clipboard': Icons.assignment_outlined,
    'camera': Icons.photo_camera_outlined,
    'picking': Icons.inventory_rounded,
    'receiving': Icons.move_to_inbox_rounded,
    'movements': Icons.swap_vert_rounded,
    'inventory': Icons.inventory_2_outlined,
    'chart': Icons.insights_rounded,
    'approval': Icons.verified_outlined,
    'fleet': Icons.local_shipping_outlined,
    'orders': Icons.receipt_long_outlined,
    'product': Icons.sell_outlined,
    'cart': Icons.shopping_cart_outlined,
    'cash': Icons.point_of_sale_outlined,
    'receipt': Icons.request_quote_outlined,
    'credit': Icons.account_balance_wallet_outlined,
    'purchasing': Icons.shopping_bag_outlined,
    'support': Icons.support_agent_rounded,
    'audit': Icons.manage_search_rounded,
    'approve-hours': Icons.fact_check_outlined,
    'clock': Icons.schedule_rounded,
  };

  /// Pantalla de cada módulo. Uno que el servidor habilita pero esta versión
  /// todavía no conoce se muestra como "próximamente" en vez de fallar.
  static const _rutas = <String, String>{
    'driver.routes': '/rutas',
    'driver.mileage': '/kilometraje',
    'driver.summary': '/pendientes',
    'warehouse.picking': '/bodega/preparar',
    'warehouse.receiving': '/bodega/recepcion',
    'warehouse.movements': '/bodega/movimientos',
    'warehouse.stock': '/bodega/inventario',
    'management.dashboard': '/gestion/metricas',
    'management.approvals': '/gestion/retenidos',
    'management.routes': '/gestion/rutas',
    'management.orders': '/pedidos',
    'management.products': '/gestion/productos',
    'sales.orders': '/ventas',
    'office.settlements': '/oficina/liquidaciones',
    'office.invoices': '/oficina/facturas',
    'office.credit': '/oficina/credito',
    'office.purchasing': '/compras',
    'office.support': '/oficina/consultas',
    'office.audit': '/oficina/bitacora',
    'staff.approvals': '/personal/aprobaciones',
    'staff.attendance': '/personal/jornadas',
  };

  /// Descripciones de respaldo para capacidades guardadas por una versión
  /// anterior de la aplicación, que no traían descripción.
  static const _descripciones = <String, String>{
    'driver.routes': 'Paradas, navegación y estado de cada entrega',
    'driver.mileage': 'Iniciá o cerrá la jornada del vehículo',
    'driver.summary': 'Acciones guardadas que faltan por enviar',
  };

  static Color _color(String key) => switch (key.split('.').first) {
        'warehouse' => BrandColors.pending,
        'management' => BrandColors.redPrimary,
        'sales' => BrandColors.action,
        'office' => BrandColors.inRoute,
        'staff' => BrandColors.view,
        _ => switch (key) {
            'driver.mileage' => BrandColors.inRoute,
            'driver.summary' => BrandColors.pending,
            _ => BrandColors.redPrimary,
          },
      };

  @override
  Widget build(BuildContext context) {
    final destino = _rutas[modulo.key];
    final disponible = destino != null;
    final color = disponible ? _color(modulo.key) : BrandColors.textMuted;

    return Card(
      child: InkWell(
        onTap: disponible ? () => context.push(destino) : null,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 16, 12, 16),
          child: Row(
            children: [
              IconBadge(
                icon: _icons[modulo.icon] ?? Icons.widgets_outlined,
                color: color,
                size: 52,
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      modulo.title,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 3),
                    Text(
                      disponible
                          ? (modulo.description.isNotEmpty
                              ? modulo.description
                              : _descripciones[modulo.key] ?? 'Abrir')
                          : 'Disponible próximamente en la aplicación',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              Icon(
                disponible
                    ? Icons.chevron_right_rounded
                    : Icons.lock_clock_outlined,
                color: BrandColors.textMuted,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
