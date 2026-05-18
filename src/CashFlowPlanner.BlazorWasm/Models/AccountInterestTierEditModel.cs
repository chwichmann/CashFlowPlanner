using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class AccountInterestTierEditModel
{
    public decimal FromAmount { get; set; }

    public decimal? ToAmount { get; set; }

    public decimal AnnualRatePercent { get; set; }

    public static AccountInterestTierEditModel FromTier(AccountInterestTier tier)
    {
        return new AccountInterestTierEditModel
        {
            FromAmount = tier.FromAmount,
            ToAmount = tier.ToAmount,
            AnnualRatePercent = tier.AnnualRatePercent
        };
    }

    public AccountInterestTier ToTier()
    {
        return new AccountInterestTier
        {
            FromAmount = FromAmount,
            ToAmount = ToAmount,
            AnnualRatePercent = AnnualRatePercent
        };
    }
}