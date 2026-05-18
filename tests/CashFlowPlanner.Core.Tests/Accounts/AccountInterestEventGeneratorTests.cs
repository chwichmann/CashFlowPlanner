using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Tests.Accounts;

public sealed class AccountInterestEventGeneratorTests
{
    [Fact]
    public void GenerateEvents_YearlyActual360_GeneratesInterestEventOnYearEnd()
    {
        var account = CreateSavingsAccount(
            openingBalance: 10_000m,
            interestContracts:
            [
                CreateFlatInterestContract(
                    postingFrequency: InterestPostingFrequency.Yearly,
                    annualRatePercent: 1m)
            ]);

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            [],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var interestEvent = Assert.Single(result);

        Assert.Equal(new DateOnly(2026, 12, 31), interestEvent.Date);
        Assert.Equal(account.Id, interestEvent.ToAccountId);
        Assert.Equal(TransactionKind.ExternalIncome, interestEvent.Kind);
        Assert.Equal("Interest", interestEvent.Category);

        // Daily accrual from Jan 1 to Dec 31 inclusive = 365 days.
        // Actual/360: 10'000 * 1% * 365 / 360 = 101.388...
        // Rounded away from zero = 101.39
        Assert.Equal(101.39m, interestEvent.Amount);
    }

    [Fact]
    public void GenerateEvents_MonthlyActual360_GeneratesTwelveEvents()
    {
        var account = CreateSavingsAccount(
            openingBalance: 10_000m,
            interestContracts:
            [
                CreateFlatInterestContract(
                    postingFrequency: InterestPostingFrequency.Monthly,
                    annualRatePercent: 1m)
            ]);

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            [],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Equal(12, result.Count);
        Assert.Contains(result, x => x.Date == new DateOnly(2026, 1, 31));
        Assert.Contains(result, x => x.Date == new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void GenerateEvents_InactiveContract_GeneratesNoEvents()
    {
        var account = CreateSavingsAccount(
            openingBalance: 10_000m,
            interestContracts:
            [
                new AccountInterestContract
                {
                    Name = "Inactive interest",
                    CalculationMethod = AccountInterestCalculationMethod.FlatBalance,
                    PostingFrequency = InterestPostingFrequency.Yearly,
                    DayCountConvention = InterestDayCountConvention.Actual360,
                    StartDate = new DateOnly(2026, 1, 1),
                    IsActive = false,
                    Tiers =
                    [
                        new AccountInterestTier
                        {
                            FromAmount = 0m,
                            ToAmount = null,
                            AnnualRatePercent = 1m
                        }
                    ]
                }
            ]);

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            [],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Empty(result);
    }

    [Fact]
    public void GenerateEvents_NegativeBalance_GeneratesNoEvents()
    {
        var account = CreateSavingsAccount(
            openingBalance: -1_000m,
            interestContracts:
            [
                CreateFlatInterestContract(
                    postingFrequency: InterestPostingFrequency.Yearly,
                    annualRatePercent: 1m)
            ]);

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            [],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Empty(result);
    }

    [Fact]
    public void GenerateEvents_ExistingDepositBeforePeriod_IncreasesInterest()
    {
        var account = CreateSavingsAccount(
            openingBalance: 0m,
            interestContracts:
            [
                CreateFlatInterestContract(
                    postingFrequency: InterestPostingFrequency.Yearly,
                    annualRatePercent: 1m)
            ]);

        var existingEvents = new List<CashFlowEvent>
        {
            new()
            {
                SourceTransactionId = Guid.NewGuid(),
                Name = "Deposit",
                Date = new DateOnly(2026, 1, 1),
                Kind = TransactionKind.ExternalIncome,
                FromAccountId = null,
                ToAccountId = account.Id,
                Amount = 10_000m,
                Currency = "CHF",
                Priority = 100
            }
        };

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            existingEvents,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var interestEvent = Assert.Single(result);

        // Deposit on Jan 1 affects balance from Jan 2 onwards because generator uses end-of-previous-day balance.
        // Jan 2 through Dec 31 inclusive = 364 days.
        // 10'000 * 1% * 364 / 360 = 101.111...
        Assert.Equal(101.11m, interestEvent.Amount);
    }

    [Fact]
    public void GenerateEvents_TieredInterest_UsesProgressiveTiers()
    {
        var account = CreateSavingsAccount(
            openingBalance: 20_000m,
            interestContracts:
            [
                new AccountInterestContract
                {
                    Name = "Youth savings interest",
                    CalculationMethod = AccountInterestCalculationMethod.TieredBalance,
                    PostingFrequency = InterestPostingFrequency.Yearly,
                    DayCountConvention = InterestDayCountConvention.Actual360,
                    StartDate = new DateOnly(2026, 1, 1),
                    Tiers =
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
                    ]
                }
            ]);

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            [],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var interestEvent = Assert.Single(result);

        // Annual tiered interest:
        // first 1'000 at 2% = 20
        // next 19'000 at 0.5% = 95
        // annual = 115
        // Actual/360 for 365 days = 115 * 365 / 360 = 116.597...
        Assert.Equal(116.60m, interestEvent.Amount);
    }

    private static Account CreateSavingsAccount(
        decimal openingBalance,
        List<AccountInterestContract> interestContracts)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = "Savings",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = new DateOnly(2026, 1, 1),
            IsActive = true,
            InterestContracts = interestContracts
        };
    }

    private static AccountInterestContract CreateFlatInterestContract(
        InterestPostingFrequency postingFrequency,
        decimal annualRatePercent)
    {
        return new AccountInterestContract
        {
            Name = "Savings interest",
            CalculationMethod = AccountInterestCalculationMethod.FlatBalance,
            PostingFrequency = postingFrequency,
            DayCountConvention = InterestDayCountConvention.Actual360,
            StartDate = new DateOnly(2026, 1, 1),
            Tiers =
            [
                new AccountInterestTier
                {
                    FromAmount = 0m,
                    ToAmount = null,
                    AnnualRatePercent = annualRatePercent
                }
            ]
        };
    }
}