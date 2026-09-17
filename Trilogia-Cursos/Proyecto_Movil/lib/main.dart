import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app.dart';
import 'core/providers.dart';
import 'core/storage/app_database.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Nombres de días y meses en español para las fechas largas de las métricas.
  await initializeDateFormatting('es');

  // Vertical unicamente: el chofer usa el telefono con una mano.
  await SystemChrome.setPreferredOrientations([
    DeviceOrientation.portraitUp,
    DeviceOrientation.portraitDown,
  ]);

  // La base se abre antes de levantar la interfaz. Si fallara, la aplicacion
  // no puede funcionar sin conexion, que es su razon de ser.
  final database = await AppDatabase.open();

  runApp(
    ProviderScope(
      overrides: [appDatabaseProvider.overrideWithValue(database)],
      child: const OperacionApp(),
    ),
  );
}
