using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

public sealed class ExpenseBudgetStatusTests
{
    [Fact]
    public void ZeroBudgetWithCommittedExpenseRequiresApproval()
    {
        var expense = new OperatingExpenseListItemViewModel { Status="Registrado", BudgetAmount=0, CommittedSpent=11300 };
        Assert.True(expense.BudgetExceeded);
        Assert.Equal("Requiere Aprobación", expense.DisplayStatus);
    }

    [Fact]
    public void ExpenseWithinBudgetKeepsRegisteredStatus()
    {
        var expense = new OperatingExpenseListItemViewModel { Status="Registrado", BudgetAmount=100, RealSpent=70, CommittedSpent=30 };
        Assert.False(expense.BudgetExceeded);
        Assert.Equal("Registrado", expense.DisplayStatus);
    }
}
