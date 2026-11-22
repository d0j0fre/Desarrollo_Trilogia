using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

/// <summary>
/// M-08: junta cada movimiento del historial salarial (fecha de registro) con su fecha de vigencia
/// (dbo.EmpleadoSalarioVigencias). Se empareja por salario nuevo y motivo, del más antiguo al más reciente,
/// y cada vigencia se usa una sola vez.
/// </summary>
public static class SalaryHistoryDates
{
    public static void Attach(IList<EmployeeSalaryHistoryViewModel> history, IReadOnlyList<EmployeeSalaryEffectiveViewModel> vigencias)
    {
        var pendientes = vigencias.OrderBy(v => v.FechaVigencia).ToList();

        foreach (var item in history.OrderBy(h => h.FechaCambio).ThenBy(h => h.HistorialSalarioId))
        {
            var match = pendientes.FirstOrDefault(v => v.Salario == item.SalarioNuevo && SameReason(v.Motivo, item.Motivo))
                        ?? pendientes.FirstOrDefault(v => v.Salario == item.SalarioNuevo);
            if (match is null) continue;
            item.FechaVigencia = match.FechaVigencia;
            pendientes.Remove(match);
        }
    }

    /// <summary>La vigencia inicial se guarda con 01/01/1900: significa «desde el alta», no una fecha real.</summary>
    public static string Describe(DateTime? fechaVigencia) =>
        fechaVigencia is null ? string.Empty
        : fechaVigencia.Value.Year <= 1900 ? "Desde el alta"
        : $"Vigente desde {fechaVigencia.Value:dd/MM/yyyy}";

    private static bool SameReason(string? a, string? b) =>
        string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
}
