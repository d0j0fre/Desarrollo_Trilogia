import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../network/api_exception.dart';
import '../theme/app_theme.dart';
import '../theme/brand_colors.dart';
import 'app_widgets.dart';

/// Piezas de pantalla compartidas por los módulos de bodega, gestión, ventas,
/// personal y oficina. Existen para que veinte pantallas se comporten igual:
/// misma búsqueda, mismos filtros, mismos estados de carga y de error.

/// Mensaje para una falla, sin códigos ni detalle técnico.
String friendlyError(Object error) {
  if (error is ApiException) {
    return switch (error.failure) {
      ApiFailure.sinConexion =>
        'No hay conexión con el servidor. Revisá la señal e intentá de nuevo.',
      ApiFailure.tiempoAgotado =>
        'El servidor está tardando en responder. Intentá de nuevo en unos segundos.',
      ApiFailure.sinPermiso => 'Tu perfil no tiene permiso para esta acción.',
      ApiFailure.servidor =>
        'El servidor no está disponible en este momento. Intentá más tarde.',
      _ => error.message,
    };
  }
  return 'Ocurrió un problema inesperado. Intentá de nuevo.';
}

void showSuccess(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Row(
        children: [
          const Icon(Icons.check_circle_rounded, color: BrandColors.white, size: 20),
          const SizedBox(width: 10),
          Expanded(child: Text(message)),
        ],
      ),
      backgroundColor: BrandColors.action,
    ));
}

void showError(BuildContext context, Object error) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Row(
        children: [
          const Icon(Icons.error_outline_rounded, color: BrandColors.white, size: 20),
          const SizedBox(width: 10),
          Expanded(child: Text(friendlyError(error))),
        ],
      ),
      backgroundColor: BrandColors.danger,
      duration: const Duration(seconds: 5),
    ));
}

/// Cuerpo de lista con carga, error y vacío resueltos, y deslizar para
/// actualizar en los tres casos.
class AsyncListBody<T> extends StatelessWidget {
  const AsyncListBody({
    super.key,
    required this.value,
    required this.onRefresh,
    required this.itemBuilder,
    required this.emptyIcon,
    required this.emptyTitle,
    required this.emptyMessage,
    this.header,
    this.padding = const EdgeInsets.fromLTRB(16, 12, 16, 32),
    this.spacing = 12,
  });

  final AsyncValue<List<T>> value;
  final Future<void> Function() onRefresh;
  final Widget Function(BuildContext context, T item) itemBuilder;
  final IconData emptyIcon;
  final String emptyTitle;
  final String emptyMessage;
  final Widget? header;
  final EdgeInsets padding;
  final double spacing;

  @override
  Widget build(BuildContext context) {
    Widget fill(Widget child) => LayoutBuilder(
          builder: (context, constraints) => ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: EdgeInsets.zero,
            children: [
              ?header,
              ConstrainedBox(
                constraints: BoxConstraints(minHeight: constraints.maxHeight * 0.7),
                child: child,
              ),
            ],
          ),
        );

    return RefreshIndicator(
      onRefresh: onRefresh,
      child: value.when(
        skipLoadingOnRefresh: true,
        loading: () => fill(const Center(child: CircularProgressIndicator())),
        error: (error, _) => fill(ErrorView(message: friendlyError(error), onRetry: onRefresh)),
        data: (items) => items.isEmpty
            ? fill(EmptyState(icon: emptyIcon, title: emptyTitle, message: emptyMessage))
            : ListView.separated(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: padding,
                itemCount: items.length + (header == null ? 0 : 1),
                separatorBuilder: (_, index) =>
                    SizedBox(height: header != null && index == 0 ? 4 : spacing),
                itemBuilder: (context, index) {
                  if (header != null) {
                    if (index == 0) return header!;
                    return itemBuilder(context, items[index - 1]);
                  }
                  return itemBuilder(context, items[index]);
                },
              ),
      ),
    );
  }
}

/// Cuerpo de detalle con carga y error resueltos.
class AsyncDetailBody<T> extends StatelessWidget {
  const AsyncDetailBody({
    super.key,
    required this.value,
    required this.onRefresh,
    required this.builder,
  });

  final AsyncValue<T> value;
  final Future<void> Function() onRefresh;
  final Widget Function(BuildContext context, T data) builder;

  @override
  Widget build(BuildContext context) {
    return value.when(
      skipLoadingOnRefresh: true,
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => ErrorView(message: friendlyError(error), onRetry: onRefresh),
      data: (data) => RefreshIndicator(onRefresh: onRefresh, child: builder(context, data)),
    );
  }
}

/// Buscador con espera: no dispara una consulta a Azure por cada letra.
class AppSearchField extends StatefulWidget {
  const AppSearchField({
    super.key,
    required this.hint,
    required this.onChanged,
    this.initialValue = '',
  });

  final String hint;
  final ValueChanged<String> onChanged;
  final String initialValue;

  @override
  State<AppSearchField> createState() => _AppSearchFieldState();
}

class _AppSearchFieldState extends State<AppSearchField> {
  late final TextEditingController _controller =
      TextEditingController(text: widget.initialValue);
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _changed(String value) {
    setState(() {});
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 450), () => widget.onChanged(value.trim()));
  }

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: _controller,
      onChanged: _changed,
      textInputAction: TextInputAction.search,
      onSubmitted: (value) {
        _debounce?.cancel();
        widget.onChanged(value.trim());
      },
      decoration: InputDecoration(
        hintText: widget.hint,
        prefixIcon: const Icon(Icons.search_rounded),
        fillColor: BrandColors.white,
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
        suffixIcon: _controller.text.isEmpty
            ? null
            : IconButton(
                icon: const Icon(Icons.close_rounded),
                tooltip: 'Limpiar búsqueda',
                onPressed: () {
                  _controller.clear();
                  _changed('');
                },
              ),
      ),
    );
  }
}

/// Fila de filtros de una sola elección, desplazable.
class FilterChipsBar<T> extends StatelessWidget {
  const FilterChipsBar({
    super.key,
    required this.options,
    required this.selected,
    required this.label,
    required this.onSelected,
  });

  final List<T> options;
  final T selected;
  final String Function(T option) label;
  final ValueChanged<T> onSelected;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 44,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        itemCount: options.length,
        separatorBuilder: (_, _) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final option = options[index];
          final isSelected = option == selected;
          return ChoiceChip(
            label: Text(label(option)),
            selected: isSelected,
            showCheckmark: false,
            onSelected: (_) => onSelected(option),
            labelStyle: TextStyle(
              fontWeight: FontWeight.w600,
              color: isSelected ? BrandColors.white : BrandColors.black,
            ),
            selectedColor: BrandColors.redPrimary,
            backgroundColor: BrandColors.white,
            side: BorderSide(color: isSelected ? BrandColors.redPrimary : BrandColors.border),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
          );
        },
      ),
    );
  }
}

/// Cabecera fija de una lista: buscador y filtros sobre el fondo de la pantalla.
class ListToolbar extends StatelessWidget {
  const ListToolbar({super.key, this.search, this.filters});

  final Widget? search;
  final Widget? filters;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 8, bottom: 4),
      child: Column(
        children: [
          if (search != null)
            Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: search),
          if (search != null && filters != null) const SizedBox(height: 10),
          ?filters,
        ],
      ),
    );
  }
}

/// Tarjeta tocable con el relleno estándar.
class TapCard extends StatelessWidget {
  const TapCard({super.key, required this.child, this.onTap, this.padding = const EdgeInsets.all(16)});

  final Widget child;
  final VoidCallback? onTap;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        onTap: onTap,
        child: Padding(padding: padding, child: child),
      ),
    );
  }
}

/// Indicador numérico con icono. Se usa en métricas y resúmenes.
class MetricTile extends StatelessWidget {
  const MetricTile({
    super.key,
    required this.label,
    required this.value,
    required this.icon,
    this.color = BrandColors.redPrimary,
    this.caption,
  });

  final String label;
  final String value;
  final IconData icon;
  final Color color;
  final String? caption;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                IconBadge(icon: icon, color: color, size: 34),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    label,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Text(
                value,
                style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: -0.3),
              ),
            ),
            if (caption != null) ...[
              const SizedBox(height: 2),
              Text(caption!, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall),
            ],
          ],
        ),
      ),
    );
  }
}

/// Encabezado de hoja inferior: título, subtítulo opcional y relleno estándar.
class SheetBody extends StatelessWidget {
  const SheetBody({super.key, required this.title, this.subtitle, required this.children});

  final String title;
  final String? subtitle;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(24, 0, 24, MediaQuery.viewInsetsOf(context).bottom + 16),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(title, style: Theme.of(context).textTheme.headlineSmall),
              if (subtitle != null) ...[
                const SizedBox(height: 6),
                Text(subtitle!, style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: BrandColors.textMuted)),
              ],
              const SizedBox(height: 18),
              ...children,
            ],
          ),
        ),
      ),
    );
  }
}

/// Botón principal que muestra que está trabajando y no admite doble toque.
class BusyButton extends StatelessWidget {
  const BusyButton({
    super.key,
    required this.label,
    required this.busy,
    required this.onPressed,
    this.icon,
    this.color,
  });

  final String label;
  final bool busy;
  final VoidCallback? onPressed;
  final IconData? icon;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final style = color == null ? null : ElevatedButton.styleFrom(backgroundColor: color);
    return ElevatedButton(
      style: style,
      onPressed: busy ? null : onPressed,
      child: busy
          ? const SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2.4, color: BrandColors.textMuted),
            )
          : Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (icon != null) ...[Icon(icon, size: 20), const SizedBox(width: 8)],
                Flexible(child: Text(label, overflow: TextOverflow.ellipsis)),
              ],
            ),
    );
  }
}

/// Color e icono por estado de pedido, compartidos por todas las listas.
class OrderStatusStyle {
  const OrderStatusStyle._();

  static Color color(String status) => switch (status) {
        'Entregado' => BrandColors.delivered,
        'Cancelado' || 'Rechazado' => BrandColors.danger,
        'Retenido' => BrandColors.pending,
        'EnProceso' || 'Preparando' || 'Liberado' => BrandColors.inRoute,
        'Aprobado' || 'Facturado' => BrandColors.view,
        _ => BrandColors.pending,
      };

  static IconData icon(String status) => switch (status) {
        'Entregado' => Icons.check_circle_rounded,
        'Cancelado' || 'Rechazado' => Icons.cancel_rounded,
        'Retenido' => Icons.pause_circle_rounded,
        'EnProceso' || 'Preparando' => Icons.inventory_2_rounded,
        'Liberado' || 'Aprobado' => Icons.verified_rounded,
        'Facturado' => Icons.receipt_long_rounded,
        _ => Icons.schedule_rounded,
      };
}

/// Separador de sección dentro de una tarjeta.
class CardSection extends StatelessWidget {
  const CardSection({super.key, required this.title, required this.children});

  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 14, 16, 10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(title.toUpperCase(), style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 6),
            ...children,
          ],
        ),
      ),
    );
  }
}

/// Cantidad con botones de más y menos, pensada para el pulgar.
class QuantityStepper extends StatelessWidget {
  const QuantityStepper({super.key, required this.value, required this.onChanged, this.min = 0, this.max = 99999});

  final int value;
  final ValueChanged<int> onChanged;
  final int min;
  final int max;

  @override
  Widget build(BuildContext context) {
    Widget button(IconData icon, VoidCallback? onTap, String tooltip) => IconButton.filledTonal(
          onPressed: onTap,
          tooltip: tooltip,
          icon: Icon(icon),
          style: IconButton.styleFrom(
            backgroundColor: BrandColors.surfaceAlt,
            foregroundColor: BrandColors.black,
            minimumSize: const Size(AppTheme.minTouchTarget, AppTheme.minTouchTarget),
          ),
        );

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        button(Icons.remove_rounded, value > min ? () => onChanged(value - 1) : null, 'Restar'),
        SizedBox(
          width: 52,
          child: Text('$value', textAlign: TextAlign.center, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
        ),
        button(Icons.add_rounded, value < max ? () => onChanged(value + 1) : null, 'Sumar'),
      ],
    );
  }
}
