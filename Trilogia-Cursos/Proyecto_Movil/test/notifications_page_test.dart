import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/core/theme/app_theme.dart';
import 'package:proyecto_movil/features/notifications/data/notifications_api.dart';
import 'package:proyecto_movil/features/notifications/presentation/notifications_page.dart';

/// Manual de usuario: «Marcar leídas» era texto blanco sobre la barra clara.
void main() {
  testWidgets('«Marcar leídas» contrasta con la barra superior', (tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [
        notificationsInboxProvider.overrideWith((ref) async => NotificationsInbox.fromJson({
              'noLeidas': 1,
              'items': [
                {'notificacionId': 1, 'titulo': 'Aviso', 'mensaje': 'x', 'leida': false},
              ],
            })),
      ],
      child: MaterialApp(theme: AppTheme.build(), home: const NotificationsPage()),
    ));
    await tester.pumpAndSettle();

    final label = find.text('Marcar leídas');
    expect(label, findsOneWidget);
    final textColor = tester.widget<Text>(label).style?.color ??
        DefaultTextStyle.of(tester.element(label)).style.color;
    final barColor = Theme.of(tester.element(label)).appBarTheme.backgroundColor!;

    expect(_contrast(textColor!, barColor), greaterThanOrEqualTo(4.5));
  });
}

double _contrast(Color a, Color b) {
  final la = a.computeLuminance(), lb = b.computeLuminance();
  final hi = la > lb ? la : lb, lo = la > lb ? lb : la;
  return (hi + 0.05) / (lo + 0.05);
}
