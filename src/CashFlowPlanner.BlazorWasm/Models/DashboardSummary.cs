using CashFlowPlanner.Core.Indexation;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class DashboardSummary
{
    /// <summary>
    /// Bank, savings and cash balances - money that can be spent this week.
    /// </summary>
    public decimal LiquidAssets { get; init; }

    /// <summary>
    /// Securities balances. Kept apart from <see cref="LiquidAssets"/> because a portfolio is an
    /// asset but not liquidity, and a household that confuses the two plans badly.
    /// </summary>
    public decimal InvestmentAssets { get; init; }

    /// <summary>
    /// Pillar 3a account balances. Restricted capital: real, counted, and not available before
    /// retirement.
    /// </summary>
    public decimal Pillar3aAssets { get; init; }

    /// <summary>
    /// Assumed market value of the household's properties.
    /// </summary>
    public decimal RealEstateValue { get; init; }

    public decimal TotalAssets { get; init; }

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

    /// <summary>
    /// Which money these figures are expressed in. Every number the engine produces is nominal -
    /// francs of the day the money moves - and <see cref="AmountBasis.Real"/> deflates them to the
    /// plan's inflation base date. A figure shown without saying which is worse than one that
    /// never offered the choice.
    /// </summary>
    public AmountBasis Basis { get; init; }

    /// <summary>
    /// The date the balance-sheet figures are taken on.
    /// </summary>
    public DateOnly AsOf { get; init; }

    /// <summary>
    /// False when the simulation produced no net-worth series - a result built by hand, or one
    /// from before the series existed. The balance sheet is then the older account-only
    /// computation, which knows nothing about property.
    /// </summary>
    public bool HasNetWorthSeries { get; init; }
}
