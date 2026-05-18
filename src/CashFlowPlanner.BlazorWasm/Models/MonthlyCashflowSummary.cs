namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class MonthlyCashflowSummary
{
    public int Year { get; init; }

    public int Month { get; init; }

    public DateOnly MonthStart { get; init; }

    public DateOnly MonthEnd { get; init; }

    public decimal Income { get; init; }

    public decimal Expenses { get; init; }

    public decimal InternalTransfersOut { get; init; }

    public decimal InternalTransfersIn { get; init; }

    public decimal MortgagePayments { get; init; }

    public decimal NetCashflow { get; init; }

    public decimal EndLiquidBalance { get; init; }

    public int WarningCount { get; init; }

    public string Currency { get; init; } = "CHF";

    public string Label => $"{Year:D4}-{Month:D2}";
}