using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services;

/// <summary>
/// Los procedimientos lanzan sus reglas de negocio con THROW y un número desde 50000
/// («El pedido indicado no existe.», «Ya existe un activo con ese código.»…): ese texto
/// está escrito para el usuario y se muestra tal cual. Cualquier otro error es técnico
/// y se reemplaza por el mensaje genérico de la pantalla.
/// </summary>
public static class BusinessErrors
{
    public const int FirstBusinessErrorNumber = 50000;

    /// <summary>Busca el error de negocio también dentro de excepciones que lo envuelven.</summary>
    public static bool TryGetMessage(Exception? exception, out string message)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Number >= FirstBusinessErrorNumber && !string.IsNullOrWhiteSpace(sql.Message))
            {
                message = sql.Message.Trim();
                return true;
            }
        }

        message = string.Empty;
        return false;
    }

    public static string MessageOr(Exception? exception, string genericMessage) =>
        TryGetMessage(exception, out var message) ? message : genericMessage;
}
