using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services;

/// <summary>
/// M-06: mensajes de las transiciones de gastos y presupuestos. La segregación de funciones (REC-22-03: quien
/// registró un gasto, o creó un presupuesto, no puede aprobarlo) se explica; las demás reglas de negocio del
/// procedimiento se muestran tal cual; lo técnico conserva «No fue posible …».
/// </summary>
public static class FinanceApprovalMessages
{
    /// <summary>sp_Admin_TransitionOperatingExpense: «El registrador no puede aprobar su propio gasto.»</summary>
    public const int ExpenseSelfApprovalNumber = 54306;

    /// <summary>sp_Admin_TransitionBudget: «El creador no puede aprobar su propio presupuesto.»</summary>
    public const int BudgetSelfApprovalNumber = 54208;

    public const string ExpenseSelfApprovalMessage =
        "No puede aprobar este gasto porque usted mismo lo registró. Por segregación de funciones, " +
        "debe aprobarlo otra persona con permiso de aprobación.";

    public const string BudgetSelfApprovalMessage =
        "No puede aprobar este presupuesto porque usted mismo lo creó. Por segregación de funciones, " +
        "debe aprobarlo otra persona con permiso de aprobación.";

    /// <summary>Si el error es «aprobar lo propio» (gasto o presupuesto), devuelve el texto que explica la segregación.</summary>
    public static bool TrySegregation(Exception exception, out string message)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                if (sql.Number == ExpenseSelfApprovalNumber) { message = ExpenseSelfApprovalMessage; return true; }
                if (sql.Number == BudgetSelfApprovalNumber) { message = BudgetSelfApprovalMessage; return true; }
            }
        }

        message = string.Empty;
        return false;
    }

    public static string For(Exception exception, string operation) =>
        TrySegregation(exception, out var segregation)
            ? segregation
            : BusinessErrors.MessageOr(exception, $"No fue posible {operation}.");
}
