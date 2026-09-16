using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services.Mobile
{
    /// <summary>
    /// Base de los servicios de datos móviles por rol.
    ///
    /// Igual que <see cref="DriverMobileDbService"/>, estas clases solo invocan
    /// procedimientos almacenados y mapean lectores: las reglas viven en SQL.
    /// La diferencia es que aquí se lee por nombre de columna. Varios de los
    /// procedimientos que se reutilizan son del sitio web y pueden ganar
    /// columnas con el tiempo; leer por posición convertiría eso en datos
    /// cruzados sin ningún error visible.
    /// </summary>
    public abstract class MobileDbServiceBase
    {
        private readonly string _connectionString;

        protected MobileDbServiceBase(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        protected async Task<List<T>> QueryAsync<T>(
            string procedure,
            Action<SqlParameterCollection>? parameters,
            Func<SqlDataReader, T> map,
            CancellationToken cancellationToken)
        {
            var items = new List<T>();
            await ReadAsync(procedure, parameters, async reader =>
            {
                while (await reader.ReadAsync(cancellationToken)) items.Add(map(reader));
            }, cancellationToken);
            return items;
        }

        protected async Task<T?> QuerySingleAsync<T>(
            string procedure,
            Action<SqlParameterCollection>? parameters,
            Func<SqlDataReader, T> map,
            CancellationToken cancellationToken) where T : class
        {
            T? item = null;
            await ReadAsync(procedure, parameters, async reader =>
            {
                if (await reader.ReadAsync(cancellationToken)) item = map(reader);
            }, cancellationToken);
            return item;
        }

        /// <summary>Para procedimientos con varios conjuntos de resultados.</summary>
        protected async Task ReadAsync(
            string procedure,
            Action<SqlParameterCollection>? parameters,
            Func<SqlDataReader, Task> consume,
            CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(procedure, connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            parameters?.Invoke(command.Parameters);

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await consume(reader);
        }

        protected async Task ExecuteAsync(
            string procedure,
            Action<SqlParameterCollection>? parameters,
            CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(procedure, connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            parameters?.Invoke(command.Parameters);

            await connection.OpenAsync(cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>Nombre de quien actúa, tal como sale del token.</summary>
        protected static string ActorName(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "Usuario móvil" : name.Trim().Length <= 150 ? name.Trim() : name.Trim()[..150];

        protected static object DbText(string? value, int maxLength) =>
            string.IsNullOrWhiteSpace(value)
                ? DBNull.Value
                : value.Trim().Length <= maxLength ? value.Trim() : value.Trim()[..maxLength];
    }

    /// <summary>Lectura por nombre de columna, tolerante a nulos.</summary>
    public static class MobileReaderExtensions
    {
        public static bool Has(this SqlDataReader reader, string column)
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), column, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string Str(this SqlDataReader reader, string column)
        {
            if (!reader.Has(column)) return string.Empty;
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

        public static int Int(this SqlDataReader reader, string column)
        {
            if (!reader.Has(column)) return 0;
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        public static long Long(this SqlDataReader reader, string column)
        {
            if (!reader.Has(column)) return 0;
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
        }

        public static decimal Dec(this SqlDataReader reader, string column)
        {
            if (!reader.Has(column)) return 0;
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
        }

        public static bool Bool(this SqlDataReader reader, string column)
        {
            if (!reader.Has(column)) return false;
            var ordinal = reader.GetOrdinal(column);
            return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
        }

        public static DateTime? Date(this SqlDataReader reader, string column)
        {
            if (!reader.Has(column)) return null;
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            var value = reader.GetValue(ordinal);
            return value switch
            {
                DateTime dateTime => dateTime,
                DateTimeOffset offset => offset.DateTime,
                DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
                _ => Convert.ToDateTime(value)
            };
        }

        public static byte[]? Bytes(this SqlDataReader reader, string column)
        {
            if (!reader.Has(column)) return null;
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : (byte[])reader.GetValue(ordinal);
        }
    }
}
