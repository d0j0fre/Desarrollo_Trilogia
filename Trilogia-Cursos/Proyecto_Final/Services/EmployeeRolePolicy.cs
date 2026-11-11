namespace Proyecto_Final.Services;

/// <summary>
/// Jerarquía de roles para registrar y administrar cuentas desde Empleados.
/// Un Supervisor tiene EMPLEADOS_CREAR/EDITAR, pero no puede crear ni tocar cuentas con más poder
/// que la suya. El Administrador administra todos los roles. Se valida en el servidor.
/// </summary>
public static class EmployeeRolePolicy
{
    public const string Administrator = "Administrador";

    private static readonly Dictionary<string, int> Levels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Administrador"] = 100,
        ["Gerente"] = 80,
        ["Supervisor"] = 60,
        ["Cajero"] = 40,
        ["Bodeguero"] = 40,
        ["Facturador"] = 40,
        ["Crédito y Cobro"] = 40,
        ["Compras"] = 40,
        ["Soporte"] = 40,
        ["Auditor Interno"] = 40,
        ["Empleado"] = 20,
        ["Vendedor"] = 20,
        ["Chofer"] = 20,
    };

    /// <summary>Un perfil que no está en la tabla cuenta como nivel operativo medio: solo los mandos lo asignan.</summary>
    private const int UnknownLevel = 40;

    public static bool IsAdministrator(string? role) =>
        string.Equals(role?.Trim(), Administrator, StringComparison.OrdinalIgnoreCase);

    public static int LevelOf(string? role) =>
        !string.IsNullOrWhiteSpace(role) && Levels.TryGetValue(role.Trim(), out var level) ? level : UnknownLevel;

    /// <summary>Roles que quien registra puede ofrecer: todos para el Administrador, solo los de nivel menor para el resto.</summary>
    public static bool CanAssign(string? actorRole, string? targetRole)
    {
        if (string.IsNullOrWhiteSpace(actorRole) || string.IsNullOrWhiteSpace(targetRole)) return false;
        return IsAdministrator(actorRole) || LevelOf(targetRole) < LevelOf(actorRole);
    }

    /// <summary>
    /// Si quien actúa puede editar, inactivar o cambiar el rol de una cuenta que hoy tiene <paramref name="targetRole"/>.
    /// Quien no es Administrador nunca toca una cuenta de Administrador, de Gerente ni de nivel igual o superior al suyo.
    /// </summary>
    public static bool CanManage(string? actorRole, string? targetRole)
    {
        if (string.IsNullOrWhiteSpace(actorRole) || string.IsNullOrWhiteSpace(targetRole)) return false;
        return IsAdministrator(actorRole) || LevelOf(targetRole) < LevelOf(actorRole);
    }

    public static IReadOnlyList<T> FilterAssignable<T>(string? actorRole, IEnumerable<T> roles, Func<T, string> roleName) =>
        roles.Where(role => CanAssign(actorRole, roleName(role))).ToList();
}
