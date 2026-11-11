namespace Proyecto_Final.Services;

/// <summary>
/// Transiciones válidas de una solicitud de licencia (G2-24): Pendiente → Aprobada | Rechazada y
/// Aprobada → Cancelada mientras no haya empezado. Una solicitud resuelta no vuelve a Pendiente.
/// La misma regla la exige sp_Admin_UpdateEmployeeLeaveRequestStatus (migración 0266).
/// </summary>
public static class EmployeeLeaveTransitions
{
    public static IReadOnlyList<string> NextStates(string? current, DateTime startDate, DateTime today) =>
        current switch
        {
            "Pendiente" => ["Aprobada", "Rechazada"],
            "Aprobada" when startDate.Date > today.Date => ["Cancelada"],
            _ => [],
        };

    public static bool IsAllowed(string? current, string? target, DateTime startDate, DateTime today) =>
        target is not null && NextStates(current, startDate, today).Contains(target);
}
