namespace Proyecto_Final.Services;

/// <summary>
/// M-16: al despachar una ruta la pantalla confirma cuántos clientes recibieron el correo («… Se notificó al cliente
/// por correo.») y avisa en amarillo si algún envío falló o algún cliente no tiene correo. El despacho no se revierte.
/// </summary>
public static class DispatchNotificationSummary
{
    public sealed record Result(string Success, string? Warning);

    public static Result Describe(int enviados, int fallidos, int sinCorreo)
    {
        var success = enviados switch
        {
            0 => string.Empty,
            1 => " Se notificó al cliente por correo.",
            _ => $" Se notificó a {enviados} clientes por correo."
        };

        var problemas = new List<string>();
        if (fallidos > 0)
            problemas.Add(fallidos == 1 ? "no fue posible enviar el correo a 1 cliente" : $"no fue posible enviar el correo a {fallidos} clientes");
        if (sinCorreo > 0)
            problemas.Add(sinCorreo == 1 ? "1 cliente no tiene correo registrado" : $"{sinCorreo} clientes no tienen correo registrado");

        var warning = problemas.Count == 0
            ? null
            : "La ruta se despachó, pero " + string.Join(" y ", problemas) + ".";
        return new Result(success, warning);
    }
}
