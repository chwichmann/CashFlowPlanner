using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Tests.Accounts;

public sealed class AccountInterestCalculatorTests
{
    [Fact]
    public void CalculateAnnualInterest_FlatBalance_ReturnsInterestForWholeBalance()
    {
        var tiers = new List<AccountInterestTier>
        {
            new()
            {
                FromAmount = 0m,
                ToAmount = null,
                AnnualRatePercent = 1.5m
            }
        };

        var result = AccountInterestCalculator.CalculateAnnualInterest(
            10_000m,
            tiers,
            AccountInterestCalculationMethod.FlatBalance);

        Assert.Equal(150m, result);
    }

    [Fact]
    public void CalculateAnnualInterest_TieredBalance_UsesProgressiveTiers()
    {
        var tiers = CreateYouthSavingsTiers();

        var result = AccountInterestCalculator.CalculateAnnualInterest(
            20_000m,
            tiers,
            AccountInterestCalculationMethod.TieredBalance);

        // First 1'000 at 2.00% = 20
        // Next 19'000 at 0.50% = 95
        // Total = 115
        Assert.Equal(115m, result);
    }

    [Fact]
    public void CalculateAnnualInterest_TieredBalance_AboveZeroRateTier_AddsNoInterestAboveLimit()
    {
        var tiers = CreateYouthSavingsTiers();

        var result = AccountInterestCalculator.CalculateAnnualInterest(
            150_000m,
            tiers,
            AccountInterestCalculationMethod.TieredBalance);

        // First 1'000 at 2.00% = 20
        // Next 99'000 at 0.50% = 495
        // Above 100'000 at 0.00% = 0
        // Total = 515
        Assert.Equal(515m, result);
    }

    [Fact]
    public void CalculateAnnualInterest_NegativeBalance_ReturnsZero()
    {
        var tiers = CreateYouthSavingsTiers();

        var result = AccountInterestCalculator.CalculateAnnualInterest(
            -1_000m,
            tiers,
            AccountInterestCalculationMethod.TieredBalance);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateAnnualInterest_ZeroBalance_ReturnsZero()
    {
        var tiers = CreateYouthSavingsTiers();

        var result = AccountInterestCalculator.CalculateAnnualInterest(
            0m,
            tiers,
            AccountInterestCalculationMethod.TieredBalance);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateInterestForPeriod_Actual360_UsesActualDaysOver360()
    {
        var tiers = new List<AccountInterestTier>
        {
            new()
            {
                FromAmount = 0m,
                ToAmount = null,
                AnnualRatePercent = 1.0m
            }
        };

        var result = AccountInterestCalculator.CalculateInterestForPeriod(
            10_000m,
            tiers,
            AccountInterestCalculationMethod.FlatBalance,
            InterestDayCountConvention.Actual360,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1));

        var expected = 10_000m * 0.01m * 31m / 360m;

        Assert.Equal(
            Math.Round(expected, 10, MidpointRounding.AwayFromZero),
            Math.Round(result, 10, MidpointRounding.AwayFromZero));
    }

    private static List<AccountInterestTier> CreateYouthSavingsTiers()
    {
        return
        [
            new AccountInterestTier
            {
                FromAmount = 0m,
                ToAmount = 1_000m,
                AnnualRatePercent = 2.00m
            },
            new AccountInterestTier
            {
                FromAmount = 1_000m,
                ToAmount = 100_000m,
                AnnualRatePercent = 0.50m
            },
            new AccountInterestTier
            {
                FromAmount = 100_000m,
                ToAmount = null,
                AnnualRatePercent = 0.00m
            }
        ];
    }
}