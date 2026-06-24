using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Models;
using System.Data;

namespace Proyecto_FinalAPI.Services
{
    public class AccountApiDbService
    {
        private readonly string _connectionString;

        public AccountApiDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<ApiUser?> ValidateUserAsync(string email, string password)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_ValidateUser", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Correo", email.Trim());
            command.Parameters.AddWithValue("@Contrasena", password.Trim());

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var user = new ApiUser
            {
                UsuarioId = reader.GetInt32(0),
                NombreCompleto = reader.GetString(1),
                Correo = reader.GetString(2),
                PerfilNombre = reader.GetString(4),
                Activo = reader.GetBoolean(5)
            };

            return user;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_EmailExists", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Correo", email.Trim());

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
            command.Parameters.AddWithValue("@NombreCompleto", request.FullName.Trim());
            command.Parameters.AddWithValue("@Correo", request.Email.Trim());
            command.Parameters.AddWithValue("@Contrasena", request.Password);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<ApiUser?> GetUserByEmailAsync(string email)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_GetUserByEmail", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Correo", email.Trim());

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new ApiUser
            {
                UsuarioId = reader.GetInt32(0),
                NombreCompleto = reader.GetString(1),
                Correo = reader.GetString(2),
                Contrasena = reader.GetString(3),
                PerfilNombre = reader.GetString(4),
                Activo = reader.GetBoolean(5)
            };
        }

        public async Task<string> CreatePasswordResetTokenAsync(int usuarioId)
        {
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_CreatePasswordResetToken", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
            command.Parameters.AddWithValue("@Token", token);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            return token;
        }

        public async Task<PasswordResetTokenInfo?> GetValidResetTokenAsync(string token)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_GetValidResetToken", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Token", token);

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
                    passwordCommand.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    passwordCommand.Parameters.AddWithValue("@Contrasena", newPassword);
                    await passwordCommand.ExecuteNonQueryAsync();
                }

                await using (var tokenCommand = new SqlCommand("dbo.sp_Auth_UseResetToken", connection, (SqlTransaction)transaction))
                {
                    tokenCommand.CommandType = CommandType.StoredProcedure;
                    tokenCommand.Parameters.AddWithValue("@Token", token);
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