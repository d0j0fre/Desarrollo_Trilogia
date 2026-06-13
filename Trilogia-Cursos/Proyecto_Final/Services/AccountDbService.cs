using Microsoft.Data.SqlClient;
using Proyecto_Final.Models;
using System.Data;

namespace Proyecto_Final.Services
{
    public class AccountDbService
    {
        private readonly string _connectionString;

        public AccountDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<AppUser?> ValidateUserAsync(string email, string password)
        {
            const string sql = @"
                SELECT TOP 1 u.UsuarioId, u.PerfilId, p.Nombre AS PerfilNombre, u.NombreCompleto, u.Correo, u.Contrasena, u.Activo
                FROM dbo.Usuarios u
                INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
                WHERE u.Correo = @Correo AND u.Activo = 1";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Correo", email.Trim());

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var user = new AppUser
            {
                UsuarioId = reader.GetInt32(0),
                PerfilId = reader.GetInt32(1),
                PerfilNombre = reader.GetString(2),
                NombreCompleto = reader.GetString(3),
                Correo = reader.GetString(4),
                Contrasena = reader.GetString(5),
                Activo = reader.GetBoolean(6)
            };

            return user.Contrasena == password ? user : null;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            const string sql = "SELECT COUNT(1) FROM dbo.Usuarios WHERE Correo = @Correo";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Correo", email.Trim());

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            var count = Convert.ToInt32(result ?? 0);
            return count > 0;
        }

        public async Task RegisterClientAsync(string fullName, string email, string password)
        {
            const string sql = @"
                INSERT INTO dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Activo)
                VALUES (
                    (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = 'Cliente'),
                    @NombreCompleto,
                    @Correo,
                    @Contrasena,
                    1
                )";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@NombreCompleto", fullName.Trim());
            command.Parameters.AddWithValue("@Correo", email.Trim());
            command.Parameters.AddWithValue("@Contrasena", password);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }


        public async Task<ProfileEditViewModel?> GetProfileAsync(int usuarioId)
        {
            const string sql = @"
                SELECT NombreCompleto, Correo, Telefono, Direccion
                FROM dbo.Usuarios
                WHERE UsuarioId = @UsuarioId
                  AND Activo = 1";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new ProfileEditViewModel
            {
                NombreCompleto = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Correo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Telefono = reader.IsDBNull(2) ? null : reader.GetString(2),
                Direccion = reader.IsDBNull(3) ? null : reader.GetString(3)
            };
        }

        public async Task<bool> EmailExistsForOtherUserAsync(string email, int usuarioId)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM dbo.Usuarios
                WHERE Correo = @Correo
                  AND UsuarioId <> @UsuarioId";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Correo", SqlDbType.NVarChar, 150).Value = email.Trim();
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0) > 0;
        }

        public async Task UpdateProfileAsync(int usuarioId, ProfileEditViewModel model)
        {
            if (await EmailExistsForOtherUserAsync(model.Correo, usuarioId))
            {
                throw new InvalidOperationException("DuplicateEmail");
            }

            const string sql = @"
                UPDATE dbo.Usuarios
                SET NombreCompleto = @NombreCompleto,
                    Correo = @Correo,
                    Telefono = @Telefono,
                    Direccion = @Direccion
                WHERE UsuarioId = @UsuarioId
                  AND Activo = 1";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);

            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@NombreCompleto", SqlDbType.NVarChar, 150).Value = model.NombreCompleto.Trim();
            command.Parameters.Add("@Correo", SqlDbType.NVarChar, 150).Value = model.Correo.Trim();
            command.Parameters.Add("@Telefono", SqlDbType.NVarChar, 30).Value = string.IsNullOrWhiteSpace(model.Telefono) ? DBNull.Value : model.Telefono.Trim();
            command.Parameters.Add("@Direccion", SqlDbType.NVarChar, 255).Value = string.IsNullOrWhiteSpace(model.Direccion) ? DBNull.Value : model.Direccion.Trim();

            await connection.OpenAsync();
            var rows = await command.ExecuteNonQueryAsync();

            if (rows == 0)
            {
                throw new InvalidOperationException("ProfileNotFound");
            }
        }

    }
}
