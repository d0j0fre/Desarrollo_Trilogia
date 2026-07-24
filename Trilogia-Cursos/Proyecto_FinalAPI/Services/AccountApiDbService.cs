using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Models;
using System.Data;
using System.Data.Common;

namespace Proyecto_FinalAPI.Services
{
    public interface IAccountApiDbService
    {
        Task<ApiUser?> ValidateUserAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<bool> EmailExistsAsync(string email);
        Task RegisterClientAsync(RegisterRequest request);
        Task<ApiUser?> GetUserByEmailAsync(string email);
        Task<string> CreatePasswordResetTokenAsync(int usuarioId);
        Task<PasswordResetTokenInfo?> GetValidResetTokenAsync(string token);
        Task ResetPasswordAsync(int usuarioId, string token, string newPassword);
    }

    public class AccountApiDbService : IAccountApiDbService
    {
        private readonly string _connectionString;
        private readonly ILogger<AccountApiDbService> _logger;
        private readonly string _dataSource;
        private readonly string _database;

        public AccountApiDbService(IConfiguration configuration, ILogger<AccountApiDbService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
            _logger = logger;

            var builder = new SqlConnectionStringBuilder(_connectionString);
            _dataSource = builder.DataSource;
            _database = builder.InitialCatalog;
        }

        public async Task<ApiUser?> ValidateUserAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = CreateValidateUserCommand(connection, email, password);

            try
            {
                _logger.LogDebug(
                    "Ejecutando {Procedure} en {DataSource}/{Database}.",
                    command.CommandText,
                    _dataSource,
                    _database);
                await connection.OpenAsync(cancellationToken);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                    return null;

                var user = MapAuthenticatedUser(reader);
                return user.Activo ? user : null;
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Falló {Procedure} en {DataSource}/{Database}; no se registraron credenciales ni cadena de conexión.",
                    command.CommandText,
                    _dataSource,
                    _database);
                throw;
            }
        }

        internal static SqlCommand CreateValidateUserCommand(SqlConnection connection, string email, string password)
        {
            var command = new SqlCommand("dbo.sp_Auth_ValidateUser", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add(new SqlParameter("@Correo", SqlDbType.NVarChar, 150) { Value = email.Trim() });
            command.Parameters.Add(new SqlParameter("@Contrasena", SqlDbType.NVarChar, 255) { Value = password });
            return command;
        }

        internal static ApiUser MapAuthenticatedUser(DbDataReader reader)
        {
            return new ApiUser
            {
                UsuarioId = reader.GetInt32(reader.GetOrdinal("UsuarioId")),
                NombreCompleto = reader.GetString(reader.GetOrdinal("NombreCompleto")),
                Correo = reader.GetString(reader.GetOrdinal("Correo")),
                PerfilNombre = reader.GetString(reader.GetOrdinal("PerfilNombre")),
                Activo = reader.GetBoolean(reader.GetOrdinal("Activo"))
            };
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_EmailExists", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@Correo", SqlDbType.NVarChar, 200) { Value = email.Trim() });

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            var count = Convert.ToInt32(result ?? 0);

            return count > 0;
        }

        public async Task RegisterClientAsync(RegisterRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_RegisterClient", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@NombreCompleto", SqlDbType.NVarChar, 200) { Value = request.FullName.Trim() });
            command.Parameters.Add(new SqlParameter("@Correo", SqlDbType.NVarChar, 200) { Value = request.Email.Trim() });
            command.Parameters.Add(new SqlParameter("@Contrasena", SqlDbType.NVarChar, 200) { Value = request.Password });

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<ApiUser?> GetUserByEmailAsync(string email)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_GetUserByEmail", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@Correo", SqlDbType.NVarChar, 200) { Value = email.Trim() });

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new ApiUser
            {
                UsuarioId = reader.GetInt32(0),
                NombreCompleto = reader.GetString(1),
                Correo = reader.GetString(2),
                PerfilNombre = reader.GetString(reader.GetOrdinal("PerfilNombre")),
                Activo = reader.GetBoolean(reader.GetOrdinal("Activo"))
            };
        }

        public async Task<string> CreatePasswordResetTokenAsync(int usuarioId)
        {
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_CreatePasswordResetToken", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@UsuarioId", SqlDbType.Int) { Value = usuarioId });
            command.Parameters.Add(new SqlParameter("@Token", SqlDbType.NVarChar, 200) { Value = token });

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            return token;
        }

        public async Task<PasswordResetTokenInfo?> GetValidResetTokenAsync(string token)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_GetValidResetToken", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@Token", SqlDbType.NVarChar, 200) { Value = token });

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var info = new PasswordResetTokenInfo
            {
                UsuarioId = reader.GetInt32(0),
                NombreCompleto = reader.GetString(1),
                Correo = reader.GetString(2),
                Token = reader.GetString(3),
                FechaExpiracion = reader.GetDateTime(4),
                Usado = reader.GetBoolean(5)
            };

            if (info.Usado || info.FechaExpiracion < DateTime.Now)
                return null;

            return info;
        }

        public async Task ResetPasswordAsync(int usuarioId, string token, string newPassword)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                await using (var passwordCommand = new SqlCommand("dbo.sp_Auth_UpdatePassword", connection, (SqlTransaction)transaction))
                {
                    passwordCommand.CommandType = CommandType.StoredProcedure;
                    passwordCommand.Parameters.Add(new SqlParameter("@UsuarioId", SqlDbType.Int) { Value = usuarioId });
                    passwordCommand.Parameters.Add(new SqlParameter("@Contrasena", SqlDbType.NVarChar, 200) { Value = newPassword });
                    await passwordCommand.ExecuteNonQueryAsync();
                }

                await using (var tokenCommand = new SqlCommand("dbo.sp_Auth_UseResetToken", connection, (SqlTransaction)transaction))
                {
                    tokenCommand.CommandType = CommandType.StoredProcedure;
                    tokenCommand.Parameters.Add(new SqlParameter("@Token", SqlDbType.NVarChar, 200) { Value = token });
                    await tokenCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
