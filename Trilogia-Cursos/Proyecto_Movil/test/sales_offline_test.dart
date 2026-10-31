import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:proyecto_movil/core/network/api_client.dart';
import 'package:proyecto_movil/core/network/api_exception.dart';
import 'package:proyecto_movil/core/sync/sync_service.dart';
import 'package:proyecto_movil/features/sales/data/sales_api.dart';
import 'package:proyecto_movil/features/sales/data/sales_catalog_cache.dart';

class _MockApi extends Mock implements ApiClient {}

class _MockSync extends Mock implements SyncService {}

class _MemoryCache implements SalesCatalogCache {
  final Map<String, List<dynamic>> saved = {};

  @override
  Future<List<dynamic>?> read(String key) async => saved[key];

  @override
  Future<void> save(String key, List<dynamic> items) async => saved[key] = items;
}

/// BUG-07-01: sin señal el vendedor tiene que poder armar el pedido con los
/// clientes y productos que ya descargó.
void main() {
  const clientsPath = 'api/mobile/v1/sales/clients';
  const productsPath = 'api/mobile/v1/sales/products';
  final clientes = [
    {'clienteId': 1, 'nombre': 'Bar El Farolito', 'correo': 'farolito@x.test', 'telefono': '8888-0001', 'direccion': 'San José'},
    {'clienteId': 2, 'nombre': 'Súper La Esquina', 'correo': 'esquina@x.test', 'telefono': '8888-0002', 'direccion': 'Heredia'},
  ];
  final productos = [
    {'productoId': 10, 'nombre': 'Red Bull 250ml', 'categoria': 'Energéticas', 'precio': 1200, 'stock': 40},
  ];

  late _MockApi api;
  late _MemoryCache cache;
  late SalesApi sales;

  setUp(() {
    api = _MockApi();
    cache = _MemoryCache();
    sales = SalesApi(api: api, sync: _MockSync(), cache: cache);
  });

  void online(String path, List<dynamic> data) =>
      when(() => api.get<List<dynamic>>(path, query: any(named: 'query'))).thenAnswer((_) async => data);

  void offline(String path) => when(() => api.get<List<dynamic>>(path, query: any(named: 'query')))
      .thenThrow(ApiException(ApiFailure.sinConexion, 'Sin conexión'));

  test('con señal la lista completa queda guardada en el teléfono', () async {
    online(clientsPath, clientes);

    final result = await sales.clients('');

    expect(result, hasLength(2));
    expect(cache.saved['clientes'], clientes);
  });

  test('una búsqueda con señal no reemplaza la copia completa', () async {
    cache.saved['clientes'] = clientes;
    online(clientsPath, [clientes.first]);

    await sales.clients('farol');

    expect(cache.saved['clientes'], hasLength(2));
  });

  test('sin señal usa los clientes y productos guardados', () async {
    cache.saved['clientes'] = clientes;
    cache.saved['productos'] = productos;
    offline(clientsPath);
    offline(productsPath);

    final clients = await sales.clients('');
    final products = await sales.products('');

    expect(clients.map((c) => c.name), ['Bar El Farolito', 'Súper La Esquina']);
    expect(products.single.name, 'Red Bull 250ml');
  });

  test('sin señal la búsqueda se resuelve en el teléfono', () async {
    cache.saved['clientes'] = clientes;
    offline(clientsPath);

    expect((await sales.clients('ESQUINA')).single.id, 2);
    expect((await sales.clients('8888-0001')).single.id, 1);
    expect(await sales.clients('nadie'), isEmpty);
  });

  test('sin señal y sin nada guardado el error llega a la pantalla', () async {
    offline(clientsPath);

    expect(() => sales.clients(''), throwsA(isA<ApiException>()));
  });

  test('un error que no es de red no se esconde detrás de la copia', () async {
    cache.saved['clientes'] = clientes;
    when(() => api.get<List<dynamic>>(clientsPath, query: any(named: 'query')))
        .thenThrow(ApiException(ApiFailure.sinPermiso, 'Sin permiso'));

    expect(() => sales.clients(''), throwsA(isA<ApiException>()));
  });
}
