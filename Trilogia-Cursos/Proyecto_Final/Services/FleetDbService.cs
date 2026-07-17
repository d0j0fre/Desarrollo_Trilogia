using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using System.Data;

namespace Proyecto_Final.Services
{
    // CU-152 / CU-153 / CU-154 / CU-161 — Kilometraje, mantenimiento, alertas y activos.
    public class FleetDbService
    {
        private readonly string _connectionString;

        public FleetDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        // ── CU-152 Kilometraje ──────────────────────────────
        public async Task<List<MileageListItemViewModel>> GetMileageAsync(int? vehiculoId, int? choferUsuarioId)
        {
            var lista = new List<MileageListItemViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Kilometraje_List", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@VehiculoId", SqlDbType.Int).Value = vehiculoId.HasValue ? vehiculoId.Value : DBNull.Value;
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = choferUsuarioId.HasValue ? choferUsuarioId.Value : DBNull.Value;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new MileageListItemViewModel
                {
                    KilometrajeId = reader.GetInt32(0),
                    VehiculoId = reader.GetInt32(1),
                    VehiculoPlaca = reader.GetString(2),
                    ChoferNombre = reader.GetString(3),
                    Fecha = reader.GetDateTime(4),
                    KmInicial = reader.GetInt32(5),
                    KmFinal = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    Recorrido = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    Observaciones = reader.GetString(8),
                    FechaRegistro = reader.GetDateTime(9)
                });
            }
            return lista;
        }

        public async Task<int> OpenMileageAsync(MileageOpenViewModel model, int choferId, string choferNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Kilometraje_Abrir", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@VehiculoId", SqlDbType.Int).Value = model.VehiculoId;
            command.Parameters.Add("@ChoferUsuarioId", SqlDbType.Int).Value = choferId > 0 ? choferId : DBNull.Value;
            command.Parameters.Add("@ChoferNombre", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(choferNombre) ? DBNull.Value : choferNombre;
            command.Parameters.Add("@KmInicial", SqlDbType.Int).Value = model.KmInicial;
            command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(model.Observaciones) ? DBNull.Value : model.Observaciones.Trim();
            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return result is int id ? id : Convert.ToInt32(result);
        }

        public async Task CloseMileageAsync(int kilometrajeId, int kmFinal)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Kilometraje_Cerrar", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@KilometrajeId", SqlDbType.Int).Value = kilometrajeId;
            command.Parameters.Add("@KmFinal", SqlDbType.Int).Value = kmFinal;
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        // ── CU-153 Mantenimiento ────────────────────────────
        public async Task<List<MaintenanceListItemViewModel>> GetMaintenanceAsync(int? vehiculoId, string? estado)
        {
            var lista = new List<MaintenanceListItemViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Mantenimiento_List", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@VehiculoId", SqlDbType.Int).Value = vehiculoId.HasValue ? vehiculoId.Value : DBNull.Value;
            command.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value = string.IsNullOrWhiteSpace(estado) ? DBNull.Value : estado.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new MaintenanceListItemViewModel
                {
                    OrdenMantenimientoId = reader.GetInt32(0),
                    VehiculoId = reader.GetInt32(1),
                    VehiculoPlaca = reader.GetString(2),
                    Tipo = reader.GetString(3),
                    Descripcion = reader.GetString(4),
                    Taller = reader.GetString(5),
                    Costo = reader.GetDecimal(6),
                    Estado = reader.GetString(7),
                    FechaProgramada = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    FechaRealizada = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    KilometrajeProximo = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                    RegistradoPorNombre = reader.GetString(11),
                    FechaRegistro = reader.GetDateTime(12)
                });
            }
            return lista;
        }

        public async Task<int> CreateMaintenanceAsync(MaintenanceFormViewModel model, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Mantenimiento_Create", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@VehiculoId", SqlDbType.Int).Value = model.VehiculoId;
            command.Parameters.Add("@Tipo", SqlDbType.NVarChar, 20).Value = model.Tipo;
            command.Parameters.Add("@Descripcion", SqlDbType.NVarChar, 300).Value = model.Descripcion.Trim();
            command.Parameters.Add("@Taller", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(model.Taller) ? DBNull.Value : model.Taller.Trim();
            command.Parameters.Add("@Costo", SqlDbType.Decimal).Value = model.Costo;
            command.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value = model.Estado;
            command.Parameters.Add("@FechaProgramada", SqlDbType.Date).Value = model.FechaProgramada.HasValue ? model.FechaProgramada.Value.Date : DBNull.Value;
            command.Parameters.Add("@FechaRealizada", SqlDbType.Date).Value = model.FechaRealizada.HasValue ? model.FechaRealizada.Value.Date : DBNull.Value;
            command.Parameters.Add("@KilometrajeProximo", SqlDbType.Int).Value = model.KilometrajeProximo.HasValue ? model.KilometrajeProximo.Value : DBNull.Value;
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId > 0 ? usuarioId : DBNull.Value;
            command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(usuarioNombre) ? DBNull.Value : usuarioNombre;
            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return result is int id ? id : Convert.ToInt32(result);
        }

        public async Task CompleteMaintenanceAsync(int ordenId, DateTime? fechaRealizada, decimal? costo)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Mantenimiento_MarcarCompletada", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@OrdenMantenimientoId", SqlDbType.Int).Value = ordenId;
            command.Parameters.Add("@FechaRealizada", SqlDbType.Date).Value = fechaRealizada.HasValue ? fechaRealizada.Value.Date : DBNull.Value;
            command.Parameters.Add("@Costo", SqlDbType.Decimal).Value = costo.HasValue ? costo.Value : DBNull.Value;
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        // ── CU-154 Alertas y documentos ─────────────────────
        public async Task<List<FleetAlertViewModel>> GetAlertsAsync(int diasAviso)
        {
            var lista = new List<FleetAlertViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Flota_Alertas", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@DiasAviso", SqlDbType.Int).Value = diasAviso;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new FleetAlertViewModel
                {
                    Categoria = reader.GetString(0),
                    VehiculoPlaca = reader.GetString(1),
                    Detalle = reader.GetString(2),
                    Fecha = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    DiasRestantes = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Severidad = reader.GetString(5)
                });
            }
            return lista;
        }

        public async Task CreateDocumentAsync(VehicleDocumentFormViewModel model)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_VehiculoDocumento_Create", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@VehiculoId", SqlDbType.Int).Value = model.VehiculoId;
            command.Parameters.Add("@Tipo", SqlDbType.NVarChar, 40).Value = model.Tipo;
            command.Parameters.Add("@FechaVencimiento", SqlDbType.Date).Value = model.FechaVencimiento.Date;
            command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(model.Observaciones) ? DBNull.Value : model.Observaciones.Trim();
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        // ── CU-161 Activos ──────────────────────────────────
        public async Task<List<AssetListItemViewModel>> GetAssetsAsync(string? buscar, string? estado)
        {
            var lista = new List<AssetListItemViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Activos_List", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@Buscar", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(buscar) ? DBNull.Value : buscar.Trim();
            command.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value = string.IsNullOrWhiteSpace(estado) ? DBNull.Value : estado.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new AssetListItemViewModel
                {
                    ActivoId = reader.GetInt32(0),
                    CodigoActivo = reader.GetString(1),
                    Nombre = reader.GetString(2),
                    Tipo = reader.GetString(3),
                    Descripcion = reader.GetString(4),
                    Estado = reader.GetString(5),
                    ClientePrestamo = reader.GetString(6),
                    Activo = reader.GetBoolean(7),
                    FechaRegistro = reader.GetDateTime(8)
                });
            }
            return lista;
        }

        public async Task<AssetFormViewModel?> GetAssetByIdAsync(int activoId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Activos_GetById", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@ActivoId", SqlDbType.Int).Value = activoId;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new AssetFormViewModel
                {
                    ActivoId = reader.GetInt32(0),
                    CodigoActivo = reader.GetString(1),
                    Nombre = reader.GetString(2),
                    Tipo = reader.GetString(3),
                    Descripcion = reader.GetString(4),
                    Estado = reader.GetString(5),
                    ClientePrestamo = reader.GetString(6)
                };
            }
            return null;
        }

        public async Task CreateAssetAsync(AssetFormViewModel model)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Activos_Create", connection) { CommandType = CommandType.StoredProcedure };
            AddAssetParams(command, model);
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateAssetAsync(AssetFormViewModel model)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Activos_Update", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@ActivoId", SqlDbType.Int).Value = model.ActivoId;
            AddAssetParams(command, model);
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        private static void AddAssetParams(SqlCommand command, AssetFormViewModel model)
        {
            command.Parameters.Add("@CodigoActivo", SqlDbType.NVarChar, 40).Value = model.CodigoActivo.Trim();
            command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = model.Nombre.Trim();
            command.Parameters.Add("@Tipo", SqlDbType.NVarChar, 40).Value = model.Tipo;
            command.Parameters.Add("@Descripcion", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(model.Descripcion) ? DBNull.Value : model.Descripcion.Trim();
            command.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value = model.Estado;
            command.Parameters.Add("@ClientePrestamo", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(model.ClientePrestamo) ? DBNull.Value : model.ClientePrestamo.Trim();
        }
    }
}
