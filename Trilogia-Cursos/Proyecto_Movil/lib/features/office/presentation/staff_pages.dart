import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../../management/presentation/orders_pages.dart' show ReasonSheet;
import '../data/office_api.dart';

final myAttendanceProvider = FutureProvider.autoDispose<List<Attendance>>(
  (ref) => ref.watch(officeApiProvider).myAttendance(),
);

final pendingAttendanceProvider = FutureProvider.autoDispose<List<Attendance>>(
  (ref) => ref.watch(officeApiProvider).pendingAttendance(),
);

Color attendanceColor(String status) => switch (status) {
      'Aprobada' => BrandColors.delivered,
      'Rechazada' => BrandColors.danger,
      'Enviada' => BrandColors.inRoute,
      _ => BrandColors.pending,
    };

/// Mis jornadas: registrar las horas del día y ver si ya se aprobaron.
class AttendancePage extends ConsumerWidget {
  const AttendancePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final items = ref.watch(myAttendanceProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Mis jornadas')),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: BrandColors.redPrimary,
        foregroundColor: BrandColors.white,
        icon: const Icon(Icons.more_time_rounded),
        label: const Text('Registrar jornada', style: TextStyle(fontWeight: FontWeight.w700)),
        onPressed: () async {
          final saved = await showModalBottomSheet<bool>(
            context: context,
            isScrollControlled: true,
            builder: (_) => const _AttendanceSheet(),
          );
          if (saved == true) {
            ref.invalidate(myAttendanceProvider);
            if (context.mounted) showSuccess(context, 'Jornada enviada para aprobación.');
          }
        },
      ),
      body: AsyncListBody<Attendance>(
        value: items,
        onRefresh: () async => ref.invalidate(myAttendanceProvider),
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
        emptyIcon: Icons.event_available_outlined,
        emptyTitle: 'Sin jornadas registradas',
        emptyMessage: 'Registrá las horas de cada día. Tu supervisor las aprueba desde su aplicación o el sitio web.',
        header: const SectionTitle('Últimos 45 días'),
        itemBuilder: (context, item) => _AttendanceCard(item: item),
      ),
    );
  }
}

class _AttendanceCard extends StatelessWidget {
  const _AttendanceCard({required this.item, this.actions});

  final Attendance item;
  final Widget? actions;

  @override
  Widget build(BuildContext context) {
    final color = attendanceColor(item.status);
    final total = item.regular + item.overtime;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                IconBadge(icon: Icons.schedule_rounded, color: color),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(actions == null ? Fmt.date(item.date) : item.employee, style: Theme.of(context).textTheme.titleMedium),
                      Text(actions == null ? '${Fmt.hours(total)} trabajadas' : '${Fmt.date(item.date)} · ${Fmt.hours(total)} trabajadas',
                          style: Theme.of(context).textTheme.bodySmall),
                    ],
                  ),
                ),
                StatusChip(label: item.status, color: color),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                _Hours(label: 'Ordinarias', value: item.regular),
                _Hours(label: 'Extra', value: item.overtime, color: BrandColors.inRoute),
                _Hours(label: 'Ausencia', value: item.absence, color: BrandColors.pending),
              ],
            ),
            if (item.notes.isNotEmpty) ...[
              const SizedBox(height: 10),
              Text(item.notes, style: Theme.of(context).textTheme.bodySmall),
            ],
            if (item.supervisorResponse.isNotEmpty) ...[
              const SizedBox(height: 10),
              InlineNotice(message: 'Supervisor: ${item.supervisorResponse}', color: color, icon: Icons.forum_outlined),
            ],
            if (actions != null) ...[const SizedBox(height: 12), actions!],
          ],
        ),
      ),
    );
  }
}

class _Hours extends StatelessWidget {
  const _Hours({required this.label, required this.value, this.color = BrandColors.black});

  final String label;
  final double value;
  final Color color;

  @override
  Widget build(BuildContext context) => Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(Fmt.hours(value), style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: color)),
            Text(label, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      );
}

class _AttendanceSheet extends ConsumerStatefulWidget {
  const _AttendanceSheet();

  @override
  ConsumerState<_AttendanceSheet> createState() => _AttendanceSheetState();
}

class _AttendanceSheetState extends ConsumerState<_AttendanceSheet> {
  DateTime _date = DateUtils.dateOnly(DateTime.now());
  final _regular = TextEditingController(text: '8');
  final _overtime = TextEditingController(text: '0');
  final _absence = TextEditingController(text: '0');
  final _notes = TextEditingController();
  bool _saving = false;

  @override
  void dispose() {
    for (final c in [_regular, _overtime, _absence, _notes]) {
      c.dispose();
    }
    super.dispose();
  }

  double _v(TextEditingController c) => double.tryParse(c.text.replaceAll(',', '.')) ?? 0;
  double get _total => _v(_regular) + _v(_overtime) + _v(_absence);

  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      await ref.read(officeApiProvider).saveAttendance(
            date: _date,
            regular: _v(_regular),
            overtime: _v(_overtime),
            absence: _v(_absence),
            notes: _notes.text.trim(),
          );
      if (mounted) Navigator.of(context).pop(true);
    } catch (error) {
      if (mounted) {
        setState(() => _saving = false);
        showError(context, error);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final valid = _total > 0 && _total <= 24;

    Widget hoursField(String label, TextEditingController controller) => Expanded(
          child: TextField(
            controller: controller,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]')), LengthLimitingTextInputFormatter(5)],
            onChanged: (_) => setState(() {}),
            decoration: InputDecoration(labelText: label, suffixText: 'h'),
          ),
        );

    return SheetBody(
      title: 'Registrar jornada',
      subtitle: 'Se envía a tu supervisor para aprobación.',
      children: [
        TapCard(
          onTap: () async {
            final now = DateTime.now();
            final picked = await showDatePicker(
              context: context,
              initialDate: _date,
              firstDate: now.subtract(const Duration(days: 45)),
              lastDate: now,
              helpText: 'Fecha de la jornada',
            );
            if (picked != null) setState(() => _date = picked);
          },
          child: Row(
            children: [
              const Icon(Icons.event_outlined, color: BrandColors.redPrimary),
              const SizedBox(width: 12),
              Expanded(child: Text(Fmt.date(_date), style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600))),
              const Text('Cambiar', style: TextStyle(color: BrandColors.view, fontWeight: FontWeight.w600)),
            ],
          ),
        ),
        const SizedBox(height: 14),
        Row(
          children: [
            hoursField('Ordinarias', _regular),
            const SizedBox(width: 8),
            hoursField('Extra', _overtime),
            const SizedBox(width: 8),
            hoursField('Ausencia', _absence),
          ],
        ),
        const SizedBox(height: 10),
        Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: (valid ? BrandColors.inRoute : BrandColors.danger).withValues(alpha: 0.08),
            borderRadius: BorderRadius.circular(AppTheme.radiusSmall),
          ),
          child: Text(valid ? 'Total del día: ${Fmt.hours(_total)}' : 'El total debe estar entre 0 y 24 horas.',
              style: TextStyle(fontWeight: FontWeight.w600, color: valid ? BrandColors.inRoute : BrandColors.danger)),
        ),
        const SizedBox(height: 12),
        TextField(controller: _notes, maxLength: 300, maxLines: 2, decoration: const InputDecoration(labelText: 'Observaciones (opcional)')),
        const SizedBox(height: 8),
        BusyButton(label: 'Enviar jornada', icon: Icons.send_rounded, busy: _saving, color: BrandColors.action, onPressed: valid ? _save : null),
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
      ],
    );
  }
}

/// Jornadas enviadas por el personal que esperan decisión.
class AttendanceApprovalsPage extends ConsumerWidget {
  const AttendanceApprovalsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final items = ref.watch(pendingAttendanceProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Aprobar jornadas')),
      body: AsyncListBody<Attendance>(
        value: items,
        onRefresh: () async => ref.invalidate(pendingAttendanceProvider),
        emptyIcon: Icons.fact_check_outlined,
        emptyTitle: 'Nada por aprobar',
        emptyMessage: 'Las jornadas que envíe el personal van a aparecer acá.',
        itemBuilder: (context, item) => _AttendanceCard(
          item: item,
          actions: _ResolveActions(item: item),
        ),
      ),
    );
  }
}

class _ResolveActions extends ConsumerStatefulWidget {
  const _ResolveActions({required this.item});

  final Attendance item;

  @override
  ConsumerState<_ResolveActions> createState() => _ResolveActionsState();
}

class _ResolveActionsState extends ConsumerState<_ResolveActions> {
  bool _busy = false;

  Future<void> _resolve(bool approve) async {
    String response = '';
    if (!approve) {
      final reason = await showModalBottomSheet<String>(
        context: context,
        isScrollControlled: true,
        builder: (_) => ReasonSheet(
          title: 'Rechazar jornada',
          subtitle: '${widget.item.employee} va a ver este motivo.',
          hint: 'Motivo del rechazo',
          confirmLabel: 'Rechazar',
          danger: true,
        ),
      );
      if (reason == null) return;
      response = reason;
    }
    if (!mounted) return;

    setState(() => _busy = true);
    try {
      await ref.read(officeApiProvider).resolveAttendance(widget.item, approve: approve, response: response);
      ref.invalidate(pendingAttendanceProvider);
      if (mounted) showSuccess(context, approve ? 'Jornada aprobada.' : 'Jornada rechazada.');
    } catch (error) {
      if (mounted) {
        showError(context, error);
        // Si otra persona la resolvió primero, la lista ya no es la de ahora.
        ref.invalidate(pendingAttendanceProvider);
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: OutlinedButton(
            style: OutlinedButton.styleFrom(foregroundColor: BrandColors.danger),
            onPressed: _busy ? null : () => _resolve(false),
            child: const Text('Rechazar'),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(child: BusyButton(label: 'Aprobar', busy: _busy, color: BrandColors.action, onPressed: () => _resolve(true))),
      ],
    );
  }
}
