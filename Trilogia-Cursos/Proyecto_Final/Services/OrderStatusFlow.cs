namespace Proyecto_Final.Services;

/// <summary>
/// Estados de un pedido con su nombre legible y los cambios que acepta
/// sp_Admin_UpdateOrderStatus desde la pantalla de Pedidos (G2-30).
///
/// Los códigos son los que ya guarda la base (decisión de Esteban): no se
/// renombran. EnRuta y Fallido los pone Rutas y entregas
/// (sp_Pedidos_SincronizarEstadoEntrega); Retenido, Liberado y Rechazado, la
/// aprobación del Gerente. Preparando y Facturado solo aparecen en los datos
/// demo (ningún procedimiento los asigna): se listan para poder filtrarlos,
/// pero no tienen cambios desde aquí.
/// </summary>
public static class OrderStatusFlow
{
    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        ("Pendiente", "Pendiente"),
        ("Retenido", "Retenido"),
        ("Liberado", "Liberado"),
        ("Aprobado", "Aprobado"),
        ("Preparando", "Preparando"),
        ("EnProceso", "En proceso"),
        ("EnRuta", "En ruta"),
        ("Fallido", "No entregado"),
        ("Entregado", "Entregado"),
        ("Facturado", "Facturado"),
        ("Cancelado", "Cancelado"),
        ("Rechazado", "Rechazado"),
    ];

    // Las mismas transiciones que valida sp_Admin_UpdateOrderStatus para un pedido sin factura.
    private static readonly Dictionary<string, string[]> Transitions = new(StringComparer.Ordinal)
    {
        ["Pendiente"] = ["Aprobado", "Cancelado"],
        ["Aprobado"] = ["EnProceso", "Cancelado"],
        ["EnProceso"] = ["Entregado", "Cancelado"],
        ["Entregado"] = ["Cancelado"],
        ["Retenido"] = ["Liberado", "Rechazado", "Cancelado"],
        ["Liberado"] = ["EnProceso", "Cancelado"],
    };

    public static string Name(string? code) =>
        All.FirstOrDefault(s => s.Code == code).Name ?? code ?? string.Empty;

    // Con factura, el procedimiento solo acepta Entregado (las ventas del vendedor se facturan
    // al crearse y quedan Pendiente). Desde En ruta o No entregado no se ofrece: la entrega la
    // cierra Rutas y entregas, y marcarla aquí dejaría la parada de la ruta sin cerrar.
    private static readonly string[] InvoicedToDelivered = ["Pendiente", "Aprobado", "EnProceso", "Liberado"];

    /// <summary>Estados a los que se puede pasar desde el actual, en el orden de la lista.</summary>
    public static IReadOnlyList<string> NextStates(string? current, bool hasInvoice)
    {
        if (current is null) return [];
        if (hasInvoice) return InvoicedToDelivered.Contains(current) ? ["Entregado"] : [];
        return Transitions.TryGetValue(current, out var next) ? next : [];
    }

    /// <summary>Clase de la pastilla de estado (admin-badge-soft).</summary>
    public static string BadgeClass(string? code) => code switch
    {
        "Pendiente" or "Retenido" => "warning",
        "Entregado" or "Facturado" => "success",
        "Cancelado" or "Rechazado" or "Fallido" => "danger",
        _ => "dark",
    };
}
