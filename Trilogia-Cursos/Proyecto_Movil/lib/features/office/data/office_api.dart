import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:uuid/uuid.dart';

import '../../../core/network/api_client.dart';
import '../../../core/providers.dart';
import '../../../shared/utils/formatters.dart';

// ── Jornadas ─────────────────────────────────────────────────────────────────

class Attendance {
  const Attendance({
    required this.id,
    required this.employee,
    required this.date,
    required this.regular,
    required this.overtime,
    required this.absence,
    required this.notes,
    required this.status,
    required this.supervisorResponse,
    required this.version,
  });

  final int id;
  final String employee;
  final DateTime? date;
  final double regular;
  final double overtime;
  final double absence;
  final String notes;
  final String status;
  final String supervisorResponse;
  final String version;

  factory Attendance.fromJson(Map<String, dynamic> json) => Attendance(
        id: J.integer(json['jornadaId']),
        employee: J.text(json['empleado']),
        date: J.date(json['fecha']),
        regular: J.decimal(json['horasOrdinarias']),
        overtime: J.decimal(json['horasExtra']),
        absence: J.decimal(json['horasAusencia']),
        notes: J.text(json['observaciones']),
        status: J.text(json['estado']),
        supervisorResponse: J.text(json['respuestaSupervisor']),
        version: J.text(json['version']),
      );
}

// ── Oficina ──────────────────────────────────────────────────────────────────

class Settlement {
  const Settlement({
    required this.routeId,
    required this.routeCode,
    required this.expectedCash,
    required this.expectedOther,
    required this.receivedCash,
    required this.vouchersTotal,
    required this.difference,
    required this.status,
    required this.notes,
    required this.settledBy,
    required this.settledAt,
    required this.vouchers,
  });

  final int routeId;
  final String routeCode;
  final double expectedCash;
  final double expectedOther;
  final double receivedCash;
  final double vouchersTotal;
  final double difference;
  final String status;
  final String notes;
  final String settledBy;
  final DateTime? settledAt;
  final List<(String, String, double)> vouchers;

  factory Settlement.fromJson(Map<String, dynamic> json) => Settlement(
        routeId: J.integer(json['rutaId']),
        routeCode: J.text(json['rutaCodigo']),
        expectedCash: J.decimal(json['montoEsperadoEfectivo']),
        expectedOther: J.decimal(json['montoEsperadoOtros']),
        receivedCash: J.decimal(json['montoEfectivoRecibido']),
        vouchersTotal: J.decimal(json['montoComprobantes']),
        difference: J.decimal(json['diferencia']),
        status: J.text(json['estado']),
        notes: J.text(json['observaciones']),
        settledBy: J.text(json['liquidadoPorNombre']),
        settledAt: J.date(json['fechaLiquidacion']),
        vouchers: J.list(json['comprobantes'], (m) => (J.text(m['tipo']), J.text(m['referencia']), J.decimal(m['monto']))),
      );
}

class Invoice {
  const Invoice({
    required this.id,
    required this.orderId,
    required this.number,
    required this.client,
    required this.clientEmail,
    required this.date,
    required this.subtotal,
    required this.tax,
    required this.total,
    required this.status,
    required this.lines,
  });

  final int id;
  final int orderId;
  final String number;
  final String client;
  final String clientEmail;
  final DateTime? date;
  final double subtotal;
  final double tax;
  final double total;
  final String status;
  final List<(String, int, double, double)> lines;

  factory Invoice.fromJson(Map<String, dynamic> json) => Invoice(
        id: J.integer(json['facturaId']),
        orderId: J.integer(json['pedidoId']),
        number: J.text(json['numeroFactura']),
        client: J.text(json['clienteNombre']),
        clientEmail: J.text(json['clienteCorreo']),
        date: J.date(json['fechaFactura']),
        subtotal: J.decimal(json['subtotal']),
        tax: J.decimal(json['impuesto']),
        total: J.decimal(json['total']),
        status: J.text(json['estado']),
        lines: J.list(json['lineas'], (m) => (J.text(m['productoNombre']), J.integer(m['cantidad']), J.decimal(m['precioUnitario']), J.decimal(m['subtotal']))),
      );
}

class ClientCredit {
  const ClientCredit({
    required this.id,
    required this.name,
    required this.email,
    required this.phone,
    required this.limit,
    required this.active,
    required this.blocked,
    required this.blockReason,
    required this.debt,
    required this.available,
    required this.charges,
    required this.payments,
    required this.lastMovement,
    required this.movements,
  });

  final int id;
  final String name;
  final String email;
  final String phone;
  final double limit;
  final bool active;
  final bool blocked;
  final String blockReason;
  final double debt;
  final double available;
  final double charges;
  final double payments;
  final DateTime? lastMovement;
  final List<CreditMovement> movements;

  String get statusLabel => blocked ? 'Bloqueado' : active ? 'Activo' : limit == 0 ? 'Sin crédito' : 'Inactivo';

  factory ClientCredit.fromJson(Map<String, dynamic> json) => ClientCredit(
        id: J.integer(json['clienteId']),
        name: J.text(json['nombre']),
        email: J.text(json['correo']),
        phone: J.text(json['telefono']),
        limit: J.decimal(json['limiteCredito']),
        active: J.boolean(json['creditoActivo']),
        blocked: J.boolean(json['creditoBloqueado']),
        blockReason: J.text(json['motivoBloqueo']),
        debt: J.decimal(json['deudaActual']),
        available: J.decimal(json['creditoDisponible']),
        charges: J.decimal(json['totalCargos']),
        payments: J.decimal(json['totalAbonos']),
        lastMovement: J.date(json['ultimoMovimiento']),
        movements: J.list(json['movimientos'], CreditMovement.fromJson),
      );
}

class CreditMovement {
  const CreditMovement(this.type, this.amount, this.description, this.reference, this.by, this.date);

  final String type;
  final double amount;
  final String description;
  final String reference;
  final String by;
  final DateTime? date;

  bool get increasesDebt => type == 'Cargo' || type == 'AjustePositivo';

  factory CreditMovement.fromJson(Map<String, dynamic> json) => CreditMovement(
        J.text(json['tipoMovimiento']),
        J.decimal(json['monto']),
        J.text(json['descripcion']),
        J.text(json['referencia']),
        J.text(json['registradoPorNombre']),
        J.date(json['fechaMovimiento']),
      );
}

class Consultation {
  const Consultation({
    required this.id,
    required this.name,
    required this.email,
    required this.subject,
    required this.message,
    required this.status,
    required this.internalResponse,
    required this.attendedBy,
    required this.attendedAt,
    required this.createdAt,
  });

  final int id;
  final String name;
  final String email;
  final String subject;
  final String message;
  final String status;
  final String internalResponse;
  final String attendedBy;
  final DateTime? attendedAt;
  final DateTime? createdAt;

  factory Consultation.fromJson(Map<String, dynamic> json) => Consultation(
        id: J.integer(json['consultaId']),
        name: J.text(json['nombre']),
        email: J.text(json['correo']),
        subject: J.text(json['asunto']),
        message: J.text(json['mensaje']),
        status: J.text(json['estado']),
        internalResponse: J.text(json['respuestaInterna']),
        attendedBy: J.text(json['atendidoPorNombre']),
        attendedAt: J.date(json['fechaAtencion']),
        createdAt: J.date(json['fechaCreacion']),
      );
}

class AuditEntry {
  const AuditEntry(this.user, this.role, this.action, this.module, this.description, this.date);

  final String user;
  final String role;
  final String action;
  final String module;
  final String description;
  final DateTime? date;

  bool get fromMobile => description.startsWith('[Móvil]');

  factory AuditEntry.fromJson(Map<String, dynamic> json) => AuditEntry(
        J.text(json['usuarioNombre']),
        J.text(json['rol']),
        J.text(json['accion']),
        J.text(json['modulo']),
        J.text(json['descripcion']),
        J.date(json['fechaRegistro']),
      );
}

// ── Acceso ───────────────────────────────────────────────────────────────────

/// Jornadas del personal y consultas de oficina. En línea: registrar horas y
/// aprobarlas se hace con la respuesta del servidor a la vista.
class OfficeApi {
  OfficeApi(this._api, this._uuid);

  final ApiClient _api;
  final Uuid _uuid;

  List<T> _list<T>(List<dynamic> data, T Function(Map<String, dynamic>) map) =>
      data.map((item) => map(item as Map<String, dynamic>)).toList();

  Future<List<Attendance>> myAttendance() async =>
      _list(await _api.get<List<dynamic>>('api/mobile/v1/staff/attendance'), Attendance.fromJson);

  Future<void> saveAttendance({
    required DateTime date,
    required double regular,
    required double overtime,
    required double absence,
    required String notes,
  }) =>
      _api.post<List<dynamic>>('api/mobile/v1/staff/attendance', body: {
        'fecha': DateFormat('yyyy-MM-dd').format(date),
        'horasOrdinarias': regular,
        'horasExtra': overtime,
        'horasAusencia': absence,
        'observaciones': notes,
        'enviar': true,
        'syncGuid': _uuid.v4(),
      });

  Future<List<Attendance>> pendingAttendance() async =>
      _list(await _api.get<List<dynamic>>('api/mobile/v1/staff/attendance/pending'), Attendance.fromJson);

  Future<void> resolveAttendance(Attendance attendance, {required bool approve, String response = ''}) =>
      _api.post<List<dynamic>>('api/mobile/v1/staff/attendance/${attendance.id}/resolve', body: {
        'decision': approve ? 'Aprobada' : 'Rechazada',
        'respuesta': response,
        'version': attendance.version,
      });

  Future<List<Settlement>> settlements({String status = ''}) async => _list(
      await _api.get<List<dynamic>>('api/mobile/v1/office/settlements', query: {if (status.isNotEmpty) 'estado': status}),
      Settlement.fromJson);

  Future<Settlement> settlement(int routeId) async =>
      Settlement.fromJson(await _api.get<Map<String, dynamic>>('api/mobile/v1/office/settlements/$routeId'));

  Future<List<Invoice>> invoices() async =>
      _list(await _api.get<List<dynamic>>('api/mobile/v1/office/invoices'), Invoice.fromJson);

  Future<Invoice> invoice(int id) async =>
      Invoice.fromJson(await _api.get<Map<String, dynamic>>('api/mobile/v1/office/invoices/$id'));

  Future<List<ClientCredit>> credits({String search = '', String status = ''}) async => _list(
      await _api.get<List<dynamic>>('api/mobile/v1/office/credit', query: {
        if (search.isNotEmpty) 'buscar': search,
        if (status.isNotEmpty) 'estado': status,
      }),
      ClientCredit.fromJson);

  Future<ClientCredit> credit(int clientId) async =>
      ClientCredit.fromJson(await _api.get<Map<String, dynamic>>('api/mobile/v1/office/credit/$clientId'));

  Future<List<Consultation>> consultations({String status = '', String search = ''}) async => _list(
      await _api.get<List<dynamic>>('api/mobile/v1/office/consultations', query: {
        if (status.isNotEmpty) 'estado': status,
        if (search.isNotEmpty) 'buscar': search,
      }),
      Consultation.fromJson);

  Future<Consultation> updateConsultation(int id, String status, String response) async => Consultation.fromJson(
      await _api.post<Map<String, dynamic>>('api/mobile/v1/office/consultations/$id/status',
          body: {'estado': status, 'respuestaInterna': response}));

  Future<List<AuditEntry>> audit({String search = ''}) async => _list(
      await _api.get<List<dynamic>>('api/mobile/v1/office/audit', query: {if (search.isNotEmpty) 'buscar': search}),
      AuditEntry.fromJson);
}

final officeApiProvider = Provider<OfficeApi>(
  (ref) => OfficeApi(ref.watch(apiClientProvider), ref.watch(uuidProvider)),
);
