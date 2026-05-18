namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgageInterestRatePoint
{
    public DateOnly Date { get; init; }

    /// <summary>
    /// Percent value.
    /// Example: 0.65 means 0.65%.
    /// For SARON mortgages this is the SARON compound rate, not including the fixed margin.
    /// </summary>
    public decimal RatePercent { get; init; }
}