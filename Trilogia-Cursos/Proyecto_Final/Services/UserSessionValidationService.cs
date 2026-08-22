using Microsoft.Data.SqlClient;
using System.Data;

namespace Proyecto_Final.Services;

public sealed record UserSessionState(bool Active, string Role, string SecurityStamp);

public interface IUserSessionValidationService
{
    Task<UserSessionState?> GetCurrentStateAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed class UserSessionValidationService : IUserSessionValidationService
{
    private readonly string _connectionString;

    public UserSessionValidationService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
    }

    public async Task<UserSessionState?> GetCurrentStateAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return null;

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.sp_Auth_GetSessionState", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = userId;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new UserSessionState(
            reader.GetBoolean(reader.GetOrdinal("Activo")),
            reader.GetString(reader.GetOrdinal("PerfilNombre")),
            reader.GetGuid(reader.GetOrdinal("SecurityStamp")).ToString("D"));
    }
}
