import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:proyecto_movil/core/providers.dart';
import 'package:proyecto_movil/core/storage/evidence_file_store.dart';
import 'package:proyecto_movil/core/theme/app_theme.dart';
import 'package:proyecto_movil/features/driver_routes/data/evidence_capture.dart';
import 'package:proyecto_movil/features/driver_routes/domain/evidence_draft.dart';
import 'package:proyecto_movil/features/driver_routes/presentation/delivery_confirm_sheet.dart';
import 'package:proyecto_movil/features/driver_routes/presentation/signature_page.dart';

/// G3-04: al marcar «Entregado» la app ofrece foto y firma del cliente. Son
/// opcionales (como en la web) y lo capturado no se queda en el teléfono si el
/// chofer cancela.
void main() {
  late Directory temp;
  late _CamaraFalsa camara;
  List<EvidenceDraft>? resultado;
  var cerrada = false;

  final jpg = Uint8List.fromList([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8]);

  Future<void> abrirHoja(WidgetTester tester) async {
    temp = (await tester.runAsync(() => Directory.systemTemp.createTemp('ui_evidencia_')))!;
    camara = _CamaraFalsa(temp, jpg);
    resultado = null;
    cerrada = false;

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          evidenceCaptureProvider.overrideWithValue(camara),
          evidenceFileStoreProvider.overrideWithValue(
            EvidenceFileStore(baseDirectory: () async => temp),
          ),
        ],
        child: MaterialApp(
          theme: AppTheme.build(),
          home: Scaffold(
            body: Builder(
              builder: (context) => ElevatedButton(
                onPressed: () async {
                  resultado = await showDeliveryConfirmSheet(
                    context,
                    pedidoId: 54,
                    cliente: 'QA Cercano',
                  );
                  cerrada = true;
                },
                child: const Text('abrir'),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('abrir'));
    await tester.pumpAndSettle();
  }

  /// Estos toques escriben, leen y borran archivos de verdad: se hacen fuera del
  /// reloj falso de las pruebas, o la entrada/salida nunca termina.
  Future<void> tocar(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await tester.pump();
    await tester.runAsync(() async {
      await tester.tap(finder);
      await Future<void>.delayed(const Duration(milliseconds: 150));
      await tester.pump();
      await Future<void>.delayed(const Duration(milliseconds: 150));
    });
    await tester.pump();
  }

  List<File> archivosGuardados() {
    final dir = Directory('${temp.path}${Platform.pathSeparator}evidencias');
    return dir.existsSync() ? dir.listSync().whereType<File>().toList() : [];
  }

  tearDown(() async {
    try {
      if (temp.existsSync()) temp.deleteSync(recursive: true);
    } on FileSystemException {
      // Windows no borra un archivo que el motor de imagenes aun tiene abierto.
    }
  });

  testWidgets('ofrece Tomar foto, galería y Firma del cliente, todo opcional', (tester) async {
    await abrirHoja(tester);

    expect(find.text('Confirmar entrega'), findsOneWidget);
    expect(find.text('Evidencia (opcional)'), findsOneWidget);
    expect(find.text('Tomar foto'), findsOneWidget);
    expect(find.text('Elegir de la galería'), findsOneWidget);
    expect(find.text('Firma del cliente'), findsOneWidget);
  });

  testWidgets('se puede confirmar sin evidencia', (tester) async {
    await abrirHoja(tester);

    await tester.tap(find.byKey(const Key('confirmar_entrega')));
    await tester.pumpAndSettle();

    expect(cerrada, isTrue);
    expect(resultado, isEmpty);
  });

  testWidgets('una foto tomada queda guardada en el teléfono y se devuelve al confirmar', (tester) async {
    await abrirHoja(tester);

    await tocar(tester, find.byKey(const Key('tomar_foto')));

    expect(camara.pedidas, [EvidenceSource.camara]);
    expect(find.text('Foto'), findsOneWidget, reason: 'aparece la miniatura de la foto');
    expect(archivosGuardados(), hasLength(1));

    await tester.ensureVisible(find.byKey(const Key('confirmar_entrega')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('confirmar_entrega')));
    await tester.pumpAndSettle();

    expect(resultado, hasLength(1));
    expect(resultado!.single.tipo, EvidenceType.foto);
    expect(File(resultado!.single.path).existsSync(), isTrue);
    expect(resultado!.single.path, contains('evidencias'));
  });

  testWidgets('la galería usa la misma captura con otra fuente', (tester) async {
    await abrirHoja(tester);

    await tocar(tester, find.byKey(const Key('foto_galeria')));

    expect(camara.pedidas, [EvidenceSource.galeria]);
    expect(archivosGuardados(), hasLength(1));
  });

  testWidgets('quitar una foto borra su archivo', (tester) async {
    await abrirHoja(tester);
    await tocar(tester, find.byKey(const Key('tomar_foto')));
    expect(archivosGuardados(), hasLength(1));

    await tocar(tester, find.bySemanticsLabel('Quitar foto'));

    expect(find.text('Foto'), findsNothing);
    expect(archivosGuardados(), isEmpty);
  });

  testWidgets('cancelar descarta lo capturado: no queda ningún archivo', (tester) async {
    await abrirHoja(tester);
    await tocar(tester, find.byKey(const Key('tomar_foto')));
    expect(archivosGuardados(), hasLength(1));

    await tester.ensureVisible(find.text('Cancelar'));
    await tester.pump();
    await tester.runAsync(() async {
      await tester.tap(find.text('Cancelar'));
      // Cancelar borra los archivos (entrada/salida real) y despues cierra la hoja.
      // (Windows puede tardar en soltar un archivo que el visor acaba de leer.)
      for (var i = 0; i < 40 && (!cerrada || archivosGuardados().isNotEmpty); i++) {
        await tester.pump(const Duration(milliseconds: 100));
        await Future<void>.delayed(const Duration(milliseconds: 100));
      }
    });
    await tester.pumpAndSettle();

    expect(cerrada, isTrue);
    expect(resultado, isNull, reason: 'null significa que no se marca la entrega');
    expect(archivosGuardados(), isEmpty);
  });

  testWidgets('un formato que el servidor no acepta se rechaza con un aviso', (tester) async {
    await abrirHoja(tester);
    camara.nombre = 'foto.heic';

    await tocar(tester, find.byKey(const Key('tomar_foto')));

    expect(find.textContaining('formato de imagen no se admite'), findsOneWidget);
    expect(archivosGuardados(), isEmpty);
    expect(find.text('Foto'), findsNothing);
  });

  testWidgets('no se pasa de tres fotos', (tester) async {
    await abrirHoja(tester);

    for (var i = 0; i < 3; i++) {
      await tocar(tester, find.byKey(const Key('tomar_foto')));
    }

    final boton = tester.widget<OutlinedButton>(find.byKey(const Key('tomar_foto')));
    expect(boton.onPressed, isNull);
    expect(archivosGuardados(), hasLength(3));
  });

  testWidgets('la firma dibujada sale como PNG', (tester) async {
    Uint8List? png;
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.build(),
        home: Builder(
          builder: (context) => ElevatedButton(
            onPressed: () async => png = await Navigator.of(context).push<Uint8List>(
              MaterialPageRoute(builder: (_) => const SignaturePage(cliente: 'QA Cercano')),
            ),
            child: const Text('firmar'),
          ),
        ),
      ),
    );
    await tester.tap(find.text('firmar'));
    await tester.pumpAndSettle();

    // Sin trazo no se puede usar.
    expect(tester.widget<ElevatedButton>(find.widgetWithText(ElevatedButton, 'Usar firma')).onPressed, isNull);

    final gesto = await tester.startGesture(tester.getCenter(find.byKey(const Key('lienzo_firma'))));
    await gesto.moveBy(const Offset(60, 20));
    await gesto.moveBy(const Offset(-30, 40));
    await gesto.up();
    await tester.pump();

    await tester.runAsync(() async {
      await tester.tap(find.widgetWithText(ElevatedButton, 'Usar firma'));
      await Future<void>.delayed(const Duration(milliseconds: 500));
    });
    await tester.pumpAndSettle();

    expect(png, isNotNull);
    // Firma binaria de un PNG: es lo unico que acepta el servidor para una firma.
    expect(png!.sublist(0, 8), [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
  });
}

class _CamaraFalsa implements EvidenceCapture {
  _CamaraFalsa(this._dir, this._bytes);

  final Directory _dir;
  final Uint8List _bytes;
  final pedidas = <EvidenceSource>[];
  String nombre = 'captura.jpg';
  var _n = 0;

  @override
  Future<String?> foto(EvidenceSource fuente) async {
    pedidas.add(fuente);
    final origen = File('${_dir.path}${Platform.pathSeparator}${_n++}_$nombre')..writeAsBytesSync(_bytes);
    return origen.path;
  }
}
