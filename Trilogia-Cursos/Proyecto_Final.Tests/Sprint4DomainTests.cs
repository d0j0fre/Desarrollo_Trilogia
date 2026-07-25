using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class Sprint4DomainTests
{
    [Fact]
    public void AnnualDistribution_PreservesExactCents()
    {
        var months = BudgetRules.DistributeAnnual(1000m);
        Assert.Equal(12, months.Count);
        Assert.Equal(1000m, months.Sum());
        Assert.Equal(83.37m, months[^1]);
    }

    [Theory]
    [InlineData("Presentado", 10, 11, true)]
    [InlineData("Presentado", 10, 10, false)]
    [InlineData("Borrador", 10, 11, false)]
    public void BudgetApproval_EnforcesStateAndSeparation(string state, int creator, int approver, bool expected) =>
        Assert.Equal(expected, BudgetRules.CanApprove(state, creator, approver));

    [Fact]
    public void ExpenseTotal_IsCalculatedServerSideEquivalentWithDecimalPrecision() =>
        Assert.Equal(113.25m, ExpenseRules.CalculateTotal(100.22m, 13.03m));

    [Theory]
    [InlineData(79.99, "Normal")]
    [InlineData(80, "Advertencia")]
    [InlineData(90, "Crítico")]
    [InlineData(100, "Excedido")]
    public void ConsumptionLevels_UseRequiredThresholds(double percentage, string expected) =>
        Assert.Equal(expected, ExpenseRules.ConsumptionLevel((decimal)percentage));

    [Fact]
    public void Projection_IsDeterministicAndNotAiBased()
    {
        Assert.Equal(2400m, BudgetComparisonCalculator.Projection(800m, 4));
        Assert.Equal(25m, BudgetComparisonCalculator.Percentage(50m, 200m));
    }

    [Theory]
    [InlineData("=2+3")]
    [InlineData("+cmd|' /C calc'!A0")]
    [InlineData("-1")]
    [InlineData("@SUM(A1:A2)")]
    public void CsvExport_NeutralizesSpreadsheetFormulaInjection(string value)
    {
        var cell = CsvExportSanitizer.Cell(value);
        Assert.StartsWith("\"'", cell);
    }

    [Fact]
    public void BusinessDate_UsesCostaRicaRatherThanUtcDate()
    {
        var utc = new DateTimeOffset(2026, 7, 22, 5, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateTime(2026, 7, 21), DocumentExpirationPolicy.BusinessDate(utc));
    }

    [Theory]
    [InlineData(true, false, "Inactivo")]
    [InlineData(false, true, "No vence")]
    [InlineData(false, false, "Vencido")]
    public void DocumentExpiration_ClassifiesImportantStates(bool inactive, bool noExpiration, string expected)
    {
        var businessDate = new DateTime(2026, 7, 22);
        DateTime? expiration = noExpiration ? null : businessDate.AddDays(-1);
        Assert.Equal(expected, DocumentExpirationPolicy.Classify(!inactive, noExpiration, expiration, businessDate, 30));
    }
}
