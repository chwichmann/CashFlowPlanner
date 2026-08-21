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

    [Theory]
    // Opening date on or before the simulation start: full year, 365 days.
    // 100'000 * 1% * 365 / 360 = 1'013.888... => 1'013.89
    [InlineData(2025, 6, 1, 1_013.89)]
    [InlineData(2026, 1, 1, 1_013.89)]
    // Opened mid-year: interest only from the opening date onwards.
    // 2026-07-01 .. 2026-12-31 inclusive = 184 days.
    // 100'000 * 1% * 184 / 360 = 511.111... => 511.11
    [InlineData(2026, 7, 1, 511.11)]
    // 2026-12-01 .. 2026-12-31 inclusive = 31 days.
    // 100'000 * 1% * 31 / 360 = 86.111... => 86.11
    [InlineData(2026, 12, 1, 86.11)]
    // Opened after the simulated range: no interest at all.
    [InlineData(2027, 1, 1, 0)]
    public void GenerateEvents_OpeningBalanceOnlyCountsFromOpeningDate(
        int openingYear,
        int openingMonth,
        int openingDay,
        decimal expectedInterest)
    {
        var account = CreateSavingsAccount(
            openingBalance: 100_000m,
            interestContracts:
            [
                CreateFlatInterestContract(
                    postingFrequency: InterestPostingFrequency.Yearly,
                    annualRatePercent: 1m)
            ],
            openingDate: new DateOnly(openingYear, openingMonth, openingDay));

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            [],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        if (expectedInterest == 0m)
        {
            Assert.Empty(result);
            return;
        }

        var interestEvent = Assert.Single(result);
        Assert.Equal(expectedInterest, interestEvent.Amount);
    }

    /// <summary>
    /// Pins the two compounding rules the balance walk has to reproduce, because
    /// the O(N + D) cursor replaced a full re-scan of every event in the plan:
    ///
    /// 1. A contract sees its OWN previously posted interest (Feb accrues on
    ///    100'000 + 86.11, not on 100'000).
    /// 2. A later contract on the SAME account additionally sees the interest the
    ///    earlier contracts already posted (the second contract's Feb accrues on
    ///    100'000 + 86.11 + 86.11).
    ///
    /// Both amounts must stay different -- if they ever become equal the second
    /// contract has stopped seeing the first one's postings.
    /// </summary>
    [Fact]
    public void GenerateEvents_TwoContractsOnOneAccount_EachSeesTheInterestPostedBefore()
    {
        var account = CreateSavingsAccount(
            openingBalance: 100_000m,
            interestContracts:
            [
                CreateFlatInterestContract(
                    postingFrequency: InterestPostingFrequency.Monthly,
                    annualRatePercent: 1m),

                CreateFlatInterestContract(
                    postingFrequency: InterestPostingFrequency.Monthly,
                    annualRatePercent: 1m)
            ]);

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            [],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 28));

        Assert.Equal(4, result.Count);

        // January: nothing posted yet, so both contracts accrue on 100'000.
        // 100'000 * 1% * 31 / 360 = 86.111... => 86.11
        var januaryAmounts = result
            .Where(x => x.Date == new DateOnly(2026, 1, 31))
            .Select(x => x.Amount)
            .ToList();

        Assert.Equal([86.11m, 86.11m], januaryAmounts);

        // February, first contract:  100'086.11 * 1% * 28 / 360 = 77.844... => 77.84
        // February, second contract: 100'172.22 * 1% * 28 / 360 = 77.911... => 77.91
        var februaryAmounts = result
            .Where(x => x.Date == new DateOnly(2026, 2, 28))
            .Select(x => x.Amount)
            .ToList();

        Assert.Equal([77.84m, 77.91m], februaryAmounts);
    }

    /// <summary>
    /// The balance walk must only ever count events dated strictly BEFORE the
    /// accrual day, and must count them exactly once no matter how many posting
    /// periods the walk crosses.
    /// </summary>
    [Fact]
    public void GenerateEvents_ExistingEventsAreCountedOncePerAccrualDay()
    {
        var account = CreateSavingsAccount(
            openingBalance: 0m,
            interestContracts:
            [
                CreateFlatInterestContract(
                    postingFrequency: InterestPostingFrequency.Monthly,
                    annualRatePercent: 1m)
            ]);

        // Two deposits of 50'000, one before the range and one mid-February.
        var existingEvents = new List<CashFlowEvent>
        {
            new()
            {
                SourceTransactionId = Guid.NewGuid(),
                Name = "Deposit A",
                Date = new DateOnly(2025, 12, 20),
                Kind = TransactionKind.ExternalIncome,
                ToAccountId = account.Id,
                Amount = 50_000m,
                Currency = "CHF"
            },
            new()
            {
                SourceTransactionId = Guid.NewGuid(),
                Name = "Deposit B",
                Date = new DateOnly(2026, 2, 15),
                Kind = TransactionKind.ExternalIncome,
                ToAccountId = account.Id,
                Amount = 50_000m,
                Currency = "CHF"
            }
        };

        var generator = new AccountInterestEventGenerator();

        var result = generator.GenerateEvents(
            [account],
            existingEvents,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 28));

        // January: 50'000 for all 31 days => 50'000 * 1% * 31 / 360 = 43.055... => 43.06
        var january = result.Single(x => x.Date == new DateOnly(2026, 1, 31));
        Assert.Equal(43.06m, january.Amount);

        // February: deposit B is only visible from the day AFTER it is dated, so
        // Feb 1..15 accrue on 50'043.06 (15 days) and Feb 16..28 on 100'043.06
        // (13 days).
        //   50'043.06 * 1% * 15 / 360 = 20.8513...
        //  100'043.06 * 1% * 13 / 360 = 36.1266...
        //  total 56.9779... => 56.98
        var february = result.Single(x => x.Date == new DateOnly(2026, 2, 28));
        Assert.Equal(56.98m, february.Amount);
    }

    private static Account CreateSavingsAccount(
        decimal openingBalance,
        List<AccountInterestContract> interestContracts,
        DateOnly? openingDate = null)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = "Savings",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = openingDate ?? new DateOnly(2026, 1, 1),
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