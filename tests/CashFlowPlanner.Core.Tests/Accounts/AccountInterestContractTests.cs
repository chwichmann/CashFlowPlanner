using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Tests.Accounts;

public sealed class AccountInterestContractTests
{
    [Fact]
    public void Validate_ValidContract_DoesNotThrow()
    {
        var contract = CreateValidContract();

        contract.Validate();
    }

    [Fact]
    public void Validate_MissingName_Throws()
    {
        var contract = new AccountInterestContract
        {
            Name = "",
            StartDate = new DateOnly(2026, 1, 1),
            Tiers =
            [
                new AccountInterestTier
                {
                    FromAmount = 0m,
                    ToAmount = null,
                    AnnualRatePercent = 1m
                }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => contract.Validate());
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_Throws()
    {
        var contract = new AccountInterestContract
        {
            Name = "Invalid interest",
            StartDate = new DateOnly(2026, 2, 1),
            EndDate = new DateOnly(2026, 1, 1),
            Tiers =
            [
                new AccountInterestTier
                {
                    FromAmount = 0m,
                    ToAmount = null,
                    AnnualRatePercent = 1m
                }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => contract.Validate());
    }

    [Fact]
    public void Validate_WithoutTiers_Throws()
    {
        var contract = new AccountInterestContract
        {
            Name = "Invalid interest",
            StartDate = new DateOnly(2026, 1, 1),
            Tiers = []
        };

        Assert.Throws<InvalidOperationException>(() => contract.Validate());
    }

    [Fact]
    public void Validate_OverlappingTiers_Throws()
    {
        var contract = new AccountInterestContract
        {
            Name = "Invalid interest",
            StartDate = new DateOnly(2026, 1, 1),
            Tiers =
            [
                new AccountInterestTier
                {
                    FromAmount = 0m,
                    ToAmount = 10_000m,
                    AnnualRatePercent = 1m
                },
                new AccountInterestTier
                {
                    FromAmount = 5_000m,
                    ToAmount = 20_000m,
                    AnnualRatePercent = 0.5m
                }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => contract.Validate());
    }

    [Fact]
    public void Validate_TouchingTiers_AreAllowed()
    {
        var contract = new AccountInterestContract
        {
            Name = "Valid interest",
            StartDate = new DateOnly(2026, 1, 1),
            Tiers =
            [
                new AccountInterestTier
                {
                    FromAmount = 0m,
                    ToAmount = 10_000m,
                    AnnualRatePercent = 1m
                },
                new AccountInterestTier
                {
                    FromAmount = 10_000m,
                    ToAmount = null,
                    AnnualRatePercent = 0.5m
                }
            ]
        };

        contract.Validate();
    }

    private static AccountInterestContract CreateValidContract()
    {
        return new AccountInterestContract
        {
            Name = "Savings interest",
            CalculationMethod = AccountInterestCalculationMethod.TieredBalance,
            PostingFrequency = InterestPostingFrequency.Yearly,
            DayCountConvention = InterestDayCountConvention.Actual360,
            StartDate = new DateOnly(2026, 1, 1),
            Tiers =
            [
                new AccountInterestTier
                {
                    FromAmount = 0m,
                    ToAmount = 100_000m,
                    AnnualRatePercent = 0.5m
                },
                new AccountInterestTier
                {
                    FromAmount = 100_000m,
                    ToAmount = null,
                    AnnualRatePercent = 0m
                }
            ]
        };
    }
}
