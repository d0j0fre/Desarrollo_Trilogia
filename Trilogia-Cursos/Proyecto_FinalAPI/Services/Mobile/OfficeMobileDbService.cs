using System.Data;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public interface IOfficeMobileDbService
    {
        Task<IReadOnlyList<MobileAttendance>> GetMyAttendanceAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task SaveMyAttendanceAsync(int userId, SaveAttendanceRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileAttendance>> GetPendingAttendanceAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task ResolveAttendanceAsync(long attendanceId, string decision, string? response, byte[] version, int supervisorUserId, string supervisorName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileSettlement>> GetSettlementsAsync(string? status, CancellationToken cancellationToken = default);
        Task<MobileSettlement?> GetSettlementAsync(int routeId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileInvoice>> GetInvoicesAsync(CancellationToken cancellationToken = default);
        Task<MobileInvoice?> GetInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileClientCredit>> GetCreditsAsync(string? search, string? status, CancellationToken cancellationToken = default);
        Task<MobileClientCredit?> GetCreditAsync(int clientId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileConsultation>> GetConsultationsAsync(string? status, string? search, CancellationToken cancellationToken = default);
        Task<MobileConsultation?> GetConsultationAsync(int consultationId, CancellationToken cancellationToken = default);
        Task UpdateConsultationAsync(int consultationId, string status, string? internalResponse, int userId, string userName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MobileAuditEntry>> GetAuditAsync(string? module, string? search, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Jornadas del personal y consultas de los perfiles de oficina (caja,
    /// facturación, crédito, soporte y auditoría). Todo reutiliza los
    /// procedimientos del sitio web: la aplicación no inventa reglas nuevas
    /// para áreas que ya tienen las suyas.
    /// </summary>
    public sealed class OfficeMobileDbService : MobileDbServiceBase, IOfficeMobileDbService
    {
        public OfficeMobileDbService(IConfiguration configuration) : base(configuration) { }

        private static MobileAttendance MapAttendance(Microsoft.Data.SqlClient.SqlDataReader r)
        {
            var version = r.Bytes("VersionFila");
            return new MobileAttendance
            {
                JornadaId = r.Long("JornadaId"),
                Empleado = r.Str("NombreCompleto"),
                Fecha = r.Date("Fecha"),
                HorasOrdinarias = r.Dec("HorasOrdinarias"),
                HorasExtra = r.Dec("HorasExtra"),
                HorasAusencia = r.Dec("HorasAusencia"),
                Observaciones = r.Str("Observaciones"),
                Estado = r.Str("Estado"),
                RespuestaSupervisor = r.Str("RespuestaSupervisor"),
                Version = version is null ? string.Empty : Convert.ToBase64String(version)
            };
        }

        public async Task<IReadOnlyList<MobileAttendance>> GetMyAttendanceAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_RRHH_ListarMisJornadas", p =>
            {
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@Desde", SqlDbType.Date).Value = from.Date;
                p.Add("@Hasta", SqlDbType.Date).Value = to.Date;
            }, MapAttendance, cancellationToken);

        public Task SaveMyAttendanceAsync(
            int userId, SaveAttendanceRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
            ExecuteAsync("dbo.sp_RRHH_GuardarMiJornada", p =>
            {
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@JornadaId", SqlDbType.BigInt).Value = DBNull.Value;
                p.Add("@Fecha", SqlDbType.Date).Value = request.Fecha.Date;
                p.Add("@HorasOrdinarias", SqlDbType.Decimal).Value = request.HorasOrdinarias;
                p["@HorasOrdinarias"].Precision = 5; p["@HorasOrdinarias"].Scale = 2;
                p.Add("@HorasExtra", SqlDbType.Decimal).Value = request.HorasExtra;
                p["@HorasExtra"].Precision = 5; p["@HorasExtra"].Scale = 2;
                p.Add("@HorasAusencia", SqlDbType.Decimal).Value = request.HorasAusencia;
                p["@HorasAusencia"].Precision = 5; p["@HorasAusencia"].Scale = 2;
                p.Add("@Observaciones", SqlDbType.NVarChar, 500).Value = DbText(request.Observaciones, 500);
                p.Add("@Enviar", SqlDbType.Bit).Value = request.Enviar;
                p.Add("@IdempotencyKey", SqlDbType.UniqueIdentifier).Value = idempotencyKey;
                p.Add("@VersionFila", SqlDbType.Binary, 8).Value = DBNull.Value;
            }, cancellationToken);

        public async Task<IReadOnlyList<MobileAttendance>> GetPendingAttendanceAsync(
            DateTime from, DateTime to, CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_RRHH_ListarJornadasPendientes", p =>
            {
                p.Add("@Desde", SqlDbType.Date).Value = from.Date;
                p.Add("@Hasta", SqlDbType.Date).Value = to.Date;
            }, MapAttendance, cancellationToken);

        public Task ResolveAttendanceAsync(
            long attendanceId, string decision, string? response, byte[] version,
            int supervisorUserId, string supervisorName, CancellationToken cancellationToken = default) =>
            ExecuteAsync("dbo.sp_RRHH_ResolverJornada", p =>
            {
                p.Add("@JornadaId", SqlDbType.BigInt).Value = attendanceId;
                p.Add("@Decision", SqlDbType.NVarChar, 20).Value = decision;
                p.Add("@RespuestaSupervisor", SqlDbType.NVarChar, 500).Value = DbText(response, 500);
                p.Add("@SupervisorUsuarioId", SqlDbType.Int).Value = supervisorUserId;
                p.Add("@SupervisorNombre", SqlDbType.NVarChar, 150).Value = ActorName(supervisorName);
                p.Add("@VersionFila", SqlDbType.Binary, 8).Value = version;
            }, cancellationToken);

        private static MobileSettlement MapSettlement(Microsoft.Data.SqlClient.SqlDataReader r) => new()
        {
            RutaId = r.Int("RutaId"),
            RutaCodigo = r.Str("RutaCodigo"),
            MontoEsperadoEfectivo = r.Dec("MontoEsperadoEfectivo"),
            MontoEsperadoOtros = r.Dec("MontoEsperadoOtros"),
            MontoEfectivoRecibido = r.Dec("MontoEfectivoRecibido"),
            MontoComprobantes = r.Dec("MontoComprobantes"),
            Diferencia = r.Dec("Diferencia"),
            Estado = r.Str("Estado"),
            Observaciones = r.Str("Observaciones"),
            LiquidadoPorNombre = r.Str("LiquidadoPorNombre"),
            FechaLiquidacion = r.Date("FechaLiquidacion")
        };

        public async Task<IReadOnlyList<MobileSettlement>> GetSettlementsAsync(
            string? status, CancellationToken cancellationToken = default) =>
            (await QueryAsync("dbo.sp_LiquidacionCobros_List",
                p => p.Add("@Estado", SqlDbType.NVarChar, 20).Value = DbText(status, 20),
                MapSettlement, cancellationToken)).Take(100).ToList();

        public async Task<MobileSettlement?> GetSettlementAsync(int routeId, CancellationToken cancellationToken = default)
        {
            MobileSettlement? settlement = null;
            await ReadAsync("dbo.sp_LiquidacionCobros_GetByRuta",
                p => p.Add("@RutaId", SqlDbType.Int).Value = routeId,
                async r =>
                {
                    if (!await r.ReadAsync(cancellationToken)) return;
                    settlement = MapSettlement(r);

                    if (!await r.NextResultAsync(cancellationToken)) return;
                    while (await r.ReadAsync(cancellationToken))
                    {
                        settlement.Comprobantes.Add(new MobileSettlementVoucher
                        {
                            Tipo = r.Str("Tipo"),
                            Referencia = r.Str("Referencia"),
                            Monto = r.Dec("Monto")
                        });
                    }
                }, cancellationToken);
            return settlement;
        }

        private static MobileInvoice MapInvoice(Microsoft.Data.SqlClient.SqlDataReader r) => new()
        {
            FacturaId = r.Int("FacturaId"),
            PedidoId = r.Int("PedidoId"),
            NumeroFactura = r.Str("NumeroFactura"),
            ClienteNombre = r.Str("ClienteNombre"),
            ClienteCorreo = r.Str("ClienteCorreo"),
            FechaFactura = r.Date("FechaFactura"),
            Subtotal = r.Dec("Subtotal"),
            Impuesto = r.Dec("Impuesto"),
            Total = r.Dec("Total"),
            Estado = r.Str("Estado")
        };

        public async Task<IReadOnlyList<MobileInvoice>> GetInvoicesAsync(CancellationToken cancellationToken = default) =>
            await QueryAsync("dbo.sp_Admin_GetInvoices", null, MapInvoice, cancellationToken);

        public async Task<MobileInvoice?> GetInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await QuerySingleAsync("dbo.sp_Admin_GetInvoiceHeader",
                p => p.Add("@FacturaId", SqlDbType.Int).Value = invoiceId, MapInvoice, cancellationToken);
            if (invoice is null) return null;

            invoice.Lineas = await QueryAsync("dbo.sp_Admin_GetInvoiceLines",
                p => p.Add("@FacturaId", SqlDbType.Int).Value = invoiceId,
                r => new MobileInvoiceLine
                {
                    ProductoNombre = r.Str("ProductoNombre"),
                    Cantidad = r.Int("Cantidad"),
                    PrecioUnitario = r.Dec("PrecioUnitario"),
                    Subtotal = r.Dec("Subtotal")
                }, cancellationToken);
            return invoice;
        }

        private static MobileClientCredit MapCredit(Microsoft.Data.SqlClient.SqlDataReader r) => new()
        {
            ClienteId = r.Int("UsuarioId"),
            Nombre = r.Str("NombreCompleto"),
            Correo = r.Str("Correo"),
            Telefono = r.Str("Telefono"),
            Direccion = r.Str("Direccion"),
            LimiteCredito = r.Dec("LimiteCredito"),
            CreditoActivo = r.Bool("CreditoActivo"),
            CreditoBloqueado = r.Bool("CreditoBloqueado"),
            MotivoBloqueo = r.Str("MotivoBloqueo"),
            DeudaActual = r.Dec("DeudaActual"),
            CreditoDisponible = r.Dec("CreditoDisponible"),
            TotalCargos = r.Dec("TotalCargos"),
            TotalAbonos = r.Dec("TotalAbonos"),
            UltimoMovimiento = r.Date("UltimoMovimiento")
        };

        public async Task<IReadOnlyList<MobileClientCredit>> GetCreditsAsync(
            string? search, string? status, CancellationToken cancellationToken = default) =>
            (await QueryAsync("dbo.sp_Admin_GetClientCredits", p =>
            {
                p.Add("@Buscar", SqlDbType.NVarChar, 200).Value = DbText(search, 200);
                p.Add("@EstadoCredito", SqlDbType.NVarChar, 30).Value = DbText(status, 30);
            }, MapCredit, cancellationToken)).Take(100).ToList();

        public async Task<MobileClientCredit?> GetCreditAsync(int clientId, CancellationToken cancellationToken = default)
        {
            MobileClientCredit? credit = null;
            await ReadAsync("dbo.sp_Admin_GetClientCreditDetail",
                p => p.Add("@UsuarioId", SqlDbType.Int).Value = clientId,
                async r =>
                {
                    if (!await r.ReadAsync(cancellationToken)) return;
                    credit = MapCredit(r);

                    if (!await r.NextResultAsync(cancellationToken)) return;
                    while (await r.ReadAsync(cancellationToken) && credit.Movimientos.Count < 50)
                    {
                        credit.Movimientos.Add(new MobileCreditMovement
                        {
                            TipoMovimiento = r.Str("TipoMovimiento"),
                            Monto = r.Dec("Monto"),
                            Descripcion = r.Str("Descripcion"),
                            Referencia = r.Str("Referencia"),
                            RegistradoPorNombre = r.Str("RegistradoPorNombre"),
                            FechaMovimiento = r.Date("FechaMovimiento")
                        });
                    }
                }, cancellationToken);
            return credit;
        }

        private static MobileConsultation MapConsultation(Microsoft.Data.SqlClient.SqlDataReader r) => new()
        {
            ConsultaId = r.Int("ConsultaId"),
            Nombre = r.Str("Nombre"),
            Correo = r.Str("Correo"),
            Asunto = r.Str("Asunto"),
            Mensaje = r.Str("Mensaje"),
            Estado = r.Str("Estado"),
            RespuestaInterna = r.Str("RespuestaInterna"),
            AtendidoPorNombre = r.Str("AtendidoPorNombre"),
            FechaAtencion = r.Date("FechaAtencion"),
            FechaCreacion = r.Date("FechaCreacion")
        };

        public async Task<IReadOnlyList<MobileConsultation>> GetConsultationsAsync(
            string? status, string? search, CancellationToken cancellationToken = default) =>
            (await QueryAsync("dbo.sp_Admin_GetConsultations", p =>
            {
                p.Add("@Estado", SqlDbType.NVarChar, 30).Value = DbText(status, 30);
                p.Add("@Buscar", SqlDbType.NVarChar, 200).Value = DbText(search, 200);
            }, MapConsultation, cancellationToken)).Take(100).ToList();

        public Task<MobileConsultation?> GetConsultationAsync(int consultationId, CancellationToken cancellationToken = default) =>
            QuerySingleAsync("dbo.sp_Admin_GetConsultationById",
                p => p.Add("@ConsultaId", SqlDbType.Int).Value = consultationId, MapConsultation, cancellationToken);

        public Task UpdateConsultationAsync(
            int consultationId, string status, string? internalResponse, int userId, string userName,
            CancellationToken cancellationToken = default) =>
            ExecuteAsync("dbo.sp_Admin_UpdateConsultationStatus", p =>
            {
                p.Add("@ConsultaId", SqlDbType.Int).Value = consultationId;
                p.Add("@Estado", SqlDbType.NVarChar, 30).Value = status;
                p.Add("@RespuestaInterna", SqlDbType.NVarChar, 1000).Value = DbText(internalResponse, 1000);
                p.Add("@AtendidoPorUsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@AtendidoPorNombre", SqlDbType.NVarChar, 150).Value = ActorName(userName);
            }, cancellationToken);

        public async Task<IReadOnlyList<MobileAuditEntry>> GetAuditAsync(
            string? module, string? search, CancellationToken cancellationToken = default) =>
            (await QueryAsync("dbo.sp_Admin_GetAuditLogs", p =>
            {
                p.Add("@Modulo", SqlDbType.NVarChar, 80).Value = DbText(module, 80);
                p.Add("@Accion", SqlDbType.NVarChar, 80).Value = DBNull.Value;
                p.Add("@Buscar", SqlDbType.NVarChar, 200).Value = DbText(search, 200);
            }, r => new MobileAuditEntry
            {
                AuditoriaId = r.Long("AuditoriaId"),
                UsuarioNombre = r.Str("UsuarioNombre"),
                Rol = r.Str("Rol"),
                Accion = r.Str("Accion"),
                Modulo = r.Str("Modulo"),
                Descripcion = r.Str("Descripcion"),
                FechaRegistro = r.Date("FechaRegistro")
                // Correo, IP y agente de usuario no viajan al teléfono: no
                // hacen falta para revisar la bitácora y son datos personales.
            }, cancellationToken)).Take(100).ToList();
    }
}
