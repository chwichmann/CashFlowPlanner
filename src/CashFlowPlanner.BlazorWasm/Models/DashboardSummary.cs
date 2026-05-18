namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class DashboardSummary
{
    public decimal LiquidAssets { get; init; }

    /// <summary>
    /// Positive account-based liabilities.
    /// Example: credit card balance -1200 becomes 1200.
    /// </summary>
    public decimal AccountLiabilities { get; init; }

    /// <summary>
    /// Positive outstanding mortgage principals.
    /// </summary>
    public decimal MortgageLiabilities { get; init; }

    /// <summary>
    /// Positive total liabilities.
    /// </summary>
    public decimal TotalLiabilities { get; init; }

    public decimal NetWorth { get; init; }

    public decimal LowestLiquidBalance { get; init; }

    public DateOnly? LowestLiquidBalanceDate { get; init; }

    public int WarningCount { get; init; }

    public int CriticalWarningCount { get; init; }

    public string Currency { get; init; } = "CHF";
}