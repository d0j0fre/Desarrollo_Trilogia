import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/providers.dart';
import '../../../core/sync/outbox_entry.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../domain/mileage.dart';

/// Estado de la jornada: abierta o no. Se consulta al servidor, pero como la
/// apertura y el cierre pasan por la cola, la pantalla puede quedar
/// momentaneamente por detras de lo que el chofer ya hizo. Por eso se muestra
/// lo que esta en camino en vez de un estado contradictorio.
final openShiftProvider = FutureProvider.autoDispose<OpenShift?>(
  (ref) => ref.watch(mileageRepositoryProvider).openShift(),
);

final driverVehiclesProvider = FutureProvider.autoDispose<List<DriverVehicle>>(
  (ref) => ref.watch(mileageRepositoryProvider).vehicles(),
);

/// Apertura o cierre de jornada que todavia no salio del telefono. Se lee de la
/// base local: no consulta la red.
final pendingShiftActionProvider = FutureProvider.autoDispose<OutboxEntry?>((
  ref,
) async {
  ref.watch(syncStateProvider);
  final pendientes = await ref.watch(outboxRepositoryProvider).pendientes();
  final jornada = pendientes.where(
    (entry) => entry.tipo.startsWith('jornada_'),
  );
  return jornada.isEmpty ? null : jornada.last;
});

class MileagePage extends ConsumerWidget {
  const MileagePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final shift = ref.watch(openShiftProvider);
    final enCamino = ref.watch(pendingShiftActionProvider).value;

    // Cuando la apertura o el cierre terminan de enviarse, lo que se habia
    // consultado al servidor quedo viejo: se vuelve a preguntar una sola vez.
    ref.listen(pendingShiftActionProvider, (previo, actual) {
      if (previo?.value != null && actual.hasValue && actual.value == null) {
        ref.invalidate(openShiftProvider);
      }
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Kilometraje')),
      body: Column(
        children: [
          const SyncBanner(),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () async {
                ref.invalidate(openShiftProvider);
                ref.invalidate(driverVehiclesProvider);
              },
              child: enCamino != null
                  ? _PendingShiftNotice(entry: enCamino)
                  : shift.when(
                      loading: () =>
                          const Center(child: CircularProgressIndicator()),
                      error: (_, _) => ErrorView(
                        message:
                            'No se pudo consultar tu jornada. Revisá la señal e intentá de nuevo.',
                        onRetry: () => ref.invalidate(openShiftProvider),
                      ),
                      data: (abierta) => abierta == null
                          ? const _OpenShiftForm()
                          : _CloseShiftForm(shift: abierta),
                    ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Encabezado de las tarjetas de jornada.
class _ShiftHeader extends StatelessWidget {
  const _ShiftHeader({
    required this.icon,
    required this.color,
    required this.title,
    required this.subtitle,
    this.trailing,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        IconBadge(icon: icon, color: color, size: 52),
        const SizedBox(width: 14),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 2),
              Text(subtitle, style: Theme.of(context).textTheme.bodySmall),
            ],
          ),
        ),
        ?trailing,
      ],
    );
  }
}

class _PendingShiftNotice extends StatelessWidget {
  const _PendingShiftNotice({required this.entry});

  final OutboxEntry entry;

  @override
  Widget build(BuildContext context) {
    final abriendo = entry.tipo == 'jornada_abrir';

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _ShiftHeader(
                  icon: Icons.cloud_upload_outlined,
                  color: BrandColors.offline,
                  title: abriendo ? 'Jornada iniciada' : 'Jornada cerrada',
                  subtitle: 'Guardada en el teléfono, pendiente de enviar',
                ),
                const SizedBox(height: 16),
                InlineNotice(
                  color: BrandColors.inRoute,
                  icon: Icons.info_outline_rounded,
                  message:
                      '${entry.descripcion}. Se va a enviar sola en cuanto haya '
                      'señal; no hace falta registrarla de nuevo.',
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _OpenShiftForm extends ConsumerStatefulWidget {
  const _OpenShiftForm();

  @override
  ConsumerState<_OpenShiftForm> createState() => _OpenShiftFormState();
}

class _OpenShiftFormState extends ConsumerState<_OpenShiftForm> {
  final _formKey = GlobalKey<FormState>();
  final _kmController = TextEditingController();
  DriverVehicle? _vehiculo;
  bool _enviando = false;

  @override
  void dispose() {
    _kmController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final vehicles = ref.watch(driverVehiclesProvider);

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const _ShiftHeader(
                  icon: Icons.play_circle_outline_rounded,
                  color: BrandColors.action,
                  title: 'Iniciar jornada',
                  subtitle: 'Anotá el kilometraje del tablero antes de salir.',
                ),
                const SizedBox(height: 22),
                vehicles.when(
                  loading: () => const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                  error: (_, _) => Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const InlineNotice(
                        color: BrandColors.offline,
                        icon: Icons.wifi_off_rounded,
                        message:
                            'No se pudieron traer tus vehículos. Revisá la señal.',
                      ),
                      const SizedBox(height: 12),
                      OutlinedButton.icon(
                        onPressed: () => ref.invalidate(driverVehiclesProvider),
                        icon: const Icon(Icons.refresh_rounded, size: 20),
                        label: const Text('Reintentar'),
                      ),
                    ],
                  ),
                  data: (lista) => lista.isEmpty
                      ? const EmptyState(
                          icon: Icons.local_shipping_outlined,
                          title: 'No tenés vehículo asignado',
                          message:
                              'Solo podés registrar kilometraje del vehículo de una ruta activa tuya.',
                        )
                      : Form(
                          key: _formKey,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              DropdownButtonFormField<DriverVehicle>(
                                initialValue: _vehiculo,
                                isExpanded: true,
                                borderRadius: BorderRadius.circular(
                                  AppTheme.radiusSmall,
                                ),
                                decoration: const InputDecoration(
                                  labelText: 'Vehículo',
                                  prefixIcon: Icon(
                                    Icons.local_shipping_outlined,
                                  ),
                                ),
                                items: lista
                                    .map(
                                      (vehiculo) => DropdownMenuItem(
                                        value: vehiculo,
                                        enabled: !vehiculo.jornadaAbierta,
                                        child: Text(
                                          vehiculo.jornadaAbierta
                                              ? '${vehiculo.placa} (jornada sin cerrar)'
                                              : '${vehiculo.placa} · ${NumberFormat.decimalPattern('es_CR').format(vehiculo.kilometrajeActual)} km',
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ),
                                    )
                                    .toList(),
                                onChanged: (value) =>
                                    setState(() => _vehiculo = value),
                                validator: (value) =>
                                    value == null ? 'Elegí el vehículo.' : null,
                              ),
                              const SizedBox(height: 14),
                              TextFormField(
                                controller: _kmController,
                                keyboardType: TextInputType.number,
                                inputFormatters: [
                                  FilteringTextInputFormatter.digitsOnly,
                                ],
                                decoration: const InputDecoration(
                                  labelText: 'Kilometraje inicial',
                                  prefixIcon: Icon(Icons.speed_rounded),
                                  suffixText: 'km',
                                ),
                                validator: _validarKm,
                              ),
                              const SizedBox(height: 22),
                              ElevatedButton.icon(
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: BrandColors.action,
                                ),
                                onPressed: _enviando ? null : _abrir,
                                icon: const Icon(Icons.play_arrow_rounded),
                                label: Text(
                                  _enviando ? 'Guardando…' : 'Iniciar jornada',
                                ),
                              ),
                            ],
                          ),
                        ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  String? _validarKm(String? value) {
    final km = int.tryParse(value ?? '');
    if (km == null) return 'Escribí el kilometraje.';
    if (km < 0) return 'No puede ser negativo.';

    // Aviso temprano: el servidor tambien lo valida, pero decirlo antes de
    // enviar evita que el chofer descubra el error media hora despues, cuando
    // la cola por fin salio.
    final actual = _vehiculo?.kilometrajeActual ?? 0;
    if (km < actual) {
      return 'El vehículo marcaba $actual km. Revisá el número.';
    }
    return null;
  }

  Future<void> _abrir() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    setState(() => _enviando = true);
    FocusScope.of(context).unfocus();

    await ref
        .read(mileageRepositoryProvider)
        .abrirJornada(
          vehiculoId: _vehiculo!.vehiculoId,
          placa: _vehiculo!.placa,
          kmInicial: int.parse(_kmController.text),
        );

    if (!mounted) return;
    setState(() => _enviando = false);

    ScaffoldMessenger.of(
      context,
    ).showSnackBar(const SnackBar(content: Text('Jornada iniciada.')));
    ref.invalidate(openShiftProvider);
  }
}

class _CloseShiftForm extends ConsumerStatefulWidget {
  const _CloseShiftForm({required this.shift});

  final OpenShift shift;

  @override
  ConsumerState<_CloseShiftForm> createState() => _CloseShiftFormState();
}

class _CloseShiftFormState extends ConsumerState<_CloseShiftForm> {
  final _formKey = GlobalKey<FormState>();
  final _kmController = TextEditingController();
  bool _enviando = false;

  @override
  void dispose() {
    _kmController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final shift = widget.shift;
    final km = NumberFormat.decimalPattern('es_CR');

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const _ShiftHeader(
                  icon: Icons.timelapse_rounded,
                  color: BrandColors.inRoute,
                  title: 'Jornada en curso',
                  subtitle: 'Cerrala al volver, con el kilometraje final.',
                  trailing: StatusChip(
                    label: 'Abierta',
                    color: BrandColors.inRoute,
                    icon: Icons.circle,
                  ),
                ),
                const SizedBox(height: 14),
                const Divider(),
                const SizedBox(height: 6),
                InfoRow(
                  icon: Icons.local_shipping_outlined,
                  label: 'Vehículo',
                  value: shift.vehiculoPlaca,
                ),
                InfoRow(
                  icon: Icons.speed_rounded,
                  label: 'Kilometraje inicial',
                  value: '${km.format(shift.kmInicial)} km',
                ),
                InfoRow(
                  icon: Icons.event_outlined,
                  label: 'Inicio',
                  value: DateFormat(
                    'dd/MM/yyyy · HH:mm',
                  ).format(shift.abiertaEn),
                ),
                const SizedBox(height: 16),
                Form(
                  key: _formKey,
                  child: TextFormField(
                    controller: _kmController,
                    keyboardType: TextInputType.number,
                    inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                    decoration: const InputDecoration(
                      labelText: 'Kilometraje final',
                      prefixIcon: Icon(Icons.flag_outlined),
                      suffixText: 'km',
                    ),
                    validator: (value) {
                      final km = int.tryParse(value ?? '');
                      if (km == null) return 'Escribí el kilometraje.';
                      if (km < shift.kmInicial) {
                        return 'No puede ser menor a ${shift.kmInicial} km.';
                      }
                      return null;
                    },
                    onChanged: (_) => setState(() {}),
                  ),
                ),
                AnimatedSwitcher(
                  duration: const Duration(milliseconds: 180),
                  child: _recorrido == null
                      ? const SizedBox(width: double.infinity)
                      : Padding(
                          padding: const EdgeInsets.only(top: 12),
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 14,
                              vertical: 12,
                            ),
                            decoration: BoxDecoration(
                              color: BrandColors.action.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(
                                AppTheme.radiusSmall,
                              ),
                            ),
                            child: Row(
                              children: [
                                const Icon(
                                  Icons.route_rounded,
                                  color: BrandColors.action,
                                ),
                                const SizedBox(width: 10),
                                const Expanded(child: Text('Recorrido')),
                                Text(
                                  '${km.format(_recorrido!)} km',
                                  style: const TextStyle(
                                    fontSize: 18,
                                    fontWeight: FontWeight.w800,
                                    color: BrandColors.action,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                ),
                const SizedBox(height: 22),
                ElevatedButton.icon(
                  onPressed: _enviando ? null : _cerrar,
                  icon: const Icon(Icons.stop_circle_outlined),
                  label: Text(_enviando ? 'Guardando…' : 'Cerrar jornada'),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  int? get _recorrido {
    final km = int.tryParse(_kmController.text);
    if (km == null || km < widget.shift.kmInicial) return null;
    return km - widget.shift.kmInicial;
  }

  Future<void> _cerrar() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final confirmado = await confirmSheet(
      context,
      title: 'Cerrar jornada',
      message:
          'Vas a cerrar la jornada de ${widget.shift.vehiculoPlaca} con '
          '${_kmController.text} km. Recorrido: $_recorrido km.',
      confirmLabel: 'Cerrar jornada',
      icon: Icons.stop_circle_outlined,
    );

    if (!confirmado || !mounted) return;

    setState(() => _enviando = true);

    await ref
        .read(mileageRepositoryProvider)
        .cerrarJornada(
          kilometrajeId: widget.shift.kilometrajeId,
          placa: widget.shift.vehiculoPlaca,
          kmFinal: int.parse(_kmController.text),
        );

    if (!mounted) return;
    setState(() => _enviando = false);

    ScaffoldMessenger.of(
      context,
    ).showSnackBar(const SnackBar(content: Text('Jornada cerrada.')));
    ref.invalidate(openShiftProvider);
  }
}
