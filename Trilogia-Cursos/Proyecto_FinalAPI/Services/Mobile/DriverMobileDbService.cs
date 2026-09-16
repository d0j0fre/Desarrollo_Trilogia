using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public interface IDriverMobileDbService
    {
        Task<IReadOnlyList<DriverRouteSummary>> GetRoutesAsync(int driverUserId, CancellationToken cancellationToken = default);
        Task<DriverRouteDetail?> GetRouteAsync(int routeId, int driverUserId, CancellationToken cancellationToken = default);
        Task<UpdateDeliveryStatusResponse?> UpdateDeliveryStatusAsync(int routeOrderId, string newStatus, Guid syncGuid, string? failureReason, int driverUserId, string driverName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DriverVehicle>> GetVehiclesAsync(int driverUserId, CancellationToken cancellationToken = default);
        Task<MileageResponse> OpenMileageAsync(int driverUserId, int vehicleId, int startKm, Guid syncGuid, string? notes, CancellationToken cancellationToken = default);
        Task<MileageResponse> CloseMileageAsync(int driverUserId, int mileageId, int endKm, Guid syncGuid, CancellationToken cancellationToken = default);
        Task<OpenMileageShift?> GetOpenShiftAsync(int driverUserId, CancellationToken cancellationToken = default);
        Task<DriverDaySummary> GetDaySummaryAsync(int driverUserId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Datos del chofer para la aplicación móvil.
    ///
    /// Invoca <b>los mismos procedimientos almacenados</b> que
    /// <c>LogisticsDbService</c> del MVC. La lógica de negocio y la validación de
    /// pertenencia viven en SQL, no aquí: esta clase solo mapea lectores. Por eso
    /// duplicar el mapeo es barato y no genera dos versiones de las reglas.
    ///
    /// La paridad de nombres de procedimiento la vigila una prueba automática.
    /// </summary>
    public sealed class DriverMobileDbService : IDriverMobileDbService
    {
        private readonly string _connectionString;

        public DriverMobileDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<IReadOnlyList<DriverRouteSummary>> GetRoutesAsync(
            int driverUserId,
            CancellationToken cancellationToken = default)
        {
            var routes = new List<DriverRouteSummary>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Chofer_GetMyRoutes", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverUserId;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                routes.Add(new DriverRouteSummary
                {
                    RutaId = reader.GetInt32(0),
                    Codigo = reader.GetString(1),
                    Zona = reader.GetString(2),
                    Estado = reader.GetString(3),
                    VehiculoPlaca = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    FechaDespacho = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    TotalPedidos = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    Pendientes = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    Entregados = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
                });
            }

            return routes;
        }

        public async Task<DriverRouteDetail?> GetRouteAsync(
            int routeId,
            int driverUserId,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Chofer_GetRouteDeliveries", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@RutaId", SqlDbType.Int).Value = routeId;
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverUserId;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            DriverRouteDetail? route = null;
            if (await reader.ReadAsync(cancellationToken))
            {
                route = new DriverRouteDetail
                {
                    RutaId = reader.GetInt32(0),
                    Codigo = reader.GetString(1),
                    Zona = reader.GetString(2),
                    Estado = reader.GetString(3),
                    VehiculoPlaca = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    FechaDespacho = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                };
            }

            if (route is not null && await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    route.Entregas.Add(new DriverDelivery
                    {
                        RutaPedidoId = reader.GetInt32(0),
                        PedidoId = reader.GetInt32(1),
                        Secuencia = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        EstadoEntrega = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        MotivoFallo = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        FechaEntrega = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        Cliente = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Telefono = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                        DireccionEntrega = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        Total = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                        TotalEvidencias = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                        Latitud = reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                        Longitud = reader.IsDBNull(12) ? null : reader.GetDecimal(12)
                    });
                }
            }

            return route;
        }

        public async Task<UpdateDeliveryStatusResponse?> UpdateDeliveryStatusAsync(
            int routeOrderId,
            string newStatus,
            Guid syncGuid,
            string? failureReason,
            int driverUserId,
            string driverName,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Chofer_UpdateDeliveryStatus", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@RutaPedidoId", SqlDbType.Int).Value = routeOrderId;
            command.Parameters.Add("@NuevoEstado", SqlDbType.NVarChar, 20).Value = newStatus.Trim();
            command.Parameters.Add("@SyncGuid", SqlDbType.UniqueIdentifier).Value = syncGuid;
            command.Parameters.Add("@MotivoFallo", SqlDbType.NVarChar, 300).Value =
                string.IsNullOrWhiteSpace(failureReason) ? DBNull.Value : failureReason.Trim();
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverUserId;
            command.Parameters.Add("@ChoferNombre", SqlDbType.NVarChar, 150).Value =
                string.IsNullOrWhiteSpace(driverName) ? DBNull.Value : driverName.Trim();

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new UpdateDeliveryStatusResponse
            {
                RutaPedidoId = reader.GetInt32(0),
                EstadoEntrega = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                PedidoId = reader.GetInt32(2),
                RutaCompletada = !reader.IsDBNull(6) && reader.GetBoolean(6),
                Duplicado = !reader.IsDBNull(7) && reader.GetBoolean(7)
            };
        }

        public async Task<IReadOnlyList<DriverVehicle>> GetVehiclesAsync(
            int driverUserId,
            CancellationToken cancellationToken = default)
        {
            var vehicles = new List<DriverVehicle>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Chofer_GetVehiculosDisponibles", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverUserId;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                vehicles.Add(new DriverVehicle
                {
                    VehiculoId = reader.GetInt32(0),
                    Placa = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Descripcion = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    KilometrajeActual = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    JornadaAbierta = !reader.IsDBNull(4) && reader.GetBoolean(4)
                });
            }

            return vehicles;
        }

        public async Task<MileageResponse> OpenMileageAsync(
            int driverUserId,
            int vehicleId,
            int startKm,
            Guid syncGuid,
            string? notes,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Chofer_Kilometraje_Abrir", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverUserId;
            command.Parameters.Add("@VehiculoId", SqlDbType.Int).Value = vehicleId;
            command.Parameters.Add("@KmInicial", SqlDbType.Int).Value = startKm;
            command.Parameters.Add("@SyncGuid", SqlDbType.UniqueIdentifier).Value = syncGuid;
            command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 300).Value =
                string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim();

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("sp_Chofer_Kilometraje_Abrir no devolvió resultado.");
            }

            return new MileageResponse
            {
                KilometrajeId = reader.GetInt32(0),
                Duplicado = !reader.IsDBNull(1) && reader.GetBoolean(1)
            };
        }

        public async Task<MileageResponse> CloseMileageAsync(
            int driverUserId,
            int mileageId,
            int endKm,
            Guid syncGuid,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Chofer_Kilometraje_Cerrar", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverUserId;
            command.Parameters.Add("@KilometrajeId", SqlDbType.Int).Value = mileageId;
            command.Parameters.Add("@KmFinal", SqlDbType.Int).Value = endKm;
            command.Parameters.Add("@SyncGuid", SqlDbType.UniqueIdentifier).Value = syncGuid;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("sp_Chofer_Kilometraje_Cerrar no devolvió resultado.");
            }

            return new MileageResponse
            {
                KilometrajeId = reader.GetInt32(0),
                KmFinal = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                KmRecorridos = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Duplicado = !reader.IsDBNull(3) && reader.GetBoolean(3)
            };
        }

        public async Task<OpenMileageShift?> GetOpenShiftAsync(
            int driverUserId,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Chofer_Kilometraje_Abierto", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverUserId;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new OpenMileageShift
            {
                KilometrajeId = reader.GetInt32(0),
                VehiculoId = reader.GetInt32(1),
                VehiculoPlaca = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                KmInicial = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Fecha = reader.GetDateTime(4),
                FechaRegistro = reader.GetDateTime(5)
            };
        }

        public async Task<DriverDaySummary> GetDaySummaryAsync(
            int driverUserId,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Chofer_ResumenDia", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = driverUserId;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return new DriverDaySummary();

            return new DriverDaySummary
            {
                RutasActivas = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                EntregasPendientes = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                EntregasCompletadasHoy = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                EntregasFallidasHoy = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                JornadaAbierta = !reader.IsDBNull(4) && reader.GetBoolean(4)
            };
        }
    }
}
