import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../../../core/widgets/ui_kit.dart';
import '../../../shared/utils/formatters.dart';
import '../data/notifications_api.dart';

/// Avisos de la cuenta: ruta asignada o reasignada, entre otros.
class NotificationsPage extends ConsumerWidget {
  const NotificationsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final inbox = ref.watch(notificationsInboxProvider);
    final unread = inbox.value?.unread ?? 0;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Notificaciones'),
        actions: [
          if (unread > 0)
            TextButton(
              onPressed: () async {
                await ref.read(notificationsApiProvider).markAllRead();
                ref.invalidate(notificationsInboxProvider);
              },
              child: const Text('Marcar leídas', style: TextStyle(color: BrandColors.white)),
            ),
        ],
      ),
      body: AsyncListBody<AppNotification>(
        value: inbox.whenData((value) => value.items),
        onRefresh: () async => ref.invalidate(notificationsInboxProvider),
        spacing: 8,
        emptyIcon: Icons.notifications_none_rounded,
        emptyTitle: 'Sin notificaciones',
        emptyMessage: 'Aquí aparecen los avisos de tu cuenta, como las rutas que te asignan.',
        itemBuilder: (context, item) => Card(
          child: ListTile(
            contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            leading: IconBadge(
              icon: item.entityType == 'Ruta' ? Icons.route_rounded : Icons.notifications_rounded,
              color: item.read ? BrandColors.textMuted : BrandColors.redPrimary,
            ),
            title: Text(item.title, style: TextStyle(fontWeight: item.read ? FontWeight.w500 : FontWeight.w800)),
            subtitle: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.message),
                const SizedBox(height: 4),
                Text(Fmt.shortDateTime(item.createdAt), style: Theme.of(context).textTheme.bodySmall),
              ],
            ),
            onTap: () async {
              if (!item.read) {
                await ref.read(notificationsApiProvider).markRead(item.id);
                ref.invalidate(notificationsInboxProvider);
              }
              final location = item.appLocation;
              if (location != null && context.mounted) context.push(location);
            },
          ),
        ),
      ),
    );
  }
}
