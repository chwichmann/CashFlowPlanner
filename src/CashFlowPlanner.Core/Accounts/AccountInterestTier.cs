namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountInterestTier
{
    public decimal FromAmount { get; init; }

    public decimal? ToAmount { get; init; }

    public decimal AnnualRatePercent { get; init; }
}