using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Tests;

/// <summary>
/// End-to-end <see cref="SimulationEngine.Simulate"/> coverage for account interest.
/// The rest of <see cref="SimulationEngineTests"/> never constructs an interest
/// contract, which is why C1 and C2 survived the suite.
/// </summary>
public sealed class SimulationEngineInterestTests
{
    [Fact]
    public void Simulate_WithSingleInterestContract_PostsInterestExactlyOnce()
    {
        // 100'000 @ 1% p.a., Actual/360, 2026-01-01 .. 2026-12-31 inclusive = 365 days.
        // 100'000 * 1% * 365 / 360 = 1'013.888... => 1'013.89
        var account = CreateSavingsAccountWithInterest(
            openingBalance: 100_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var engine = new SimulationEngine();

        var result = engine.Simulate(plan);

        var interestEvents = result.Events
            .Where(x => x.Category == "Interest")
            .ToList();

        var interestEvent = Assert.Single(interestEvents);

        Assert.Equal(new DateOnly(2026, 12, 31), interestEvent.Date);
        Assert.Equal(1_013.89m, interestEvent.Amount);

        Assert.Equal(
            101_013.89m,
            result.GetBalance(account.Id, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Simulate_WithInterestContract_RunsAfterCreditCardPayments()
    {
        // Interest must be generated last: it depends on the credit-card payment
        // events, which in turn depend on the transaction events.
        var bankAccount = CreateSavingsAccountWithInterest(
            openingBalance: 100_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var creditCardAccount = TestPlanBuilder.CreateCreditCardAccount(
            openingBalance: -12_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var creditCard = new CreditCards.CreditCardContract
        {
            Id = Guid.NewGuid(),
            Name = "Visa",
            CreditCardAccountId = creditCardAccount.Id,
            PaymentAccountId = bankAccount.Id,
            ClosingDayOfMonth = 15,
            PaymentDayOfMonth = 25,
            PaymentBusinessDayAdjustment = BusinessDayAdjustment.None,
            StartDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [bankAccount, creditCardAccount],
            transactions: [],
            creditCards: [creditCard],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var engine = new SimulationEngine();

        var result = engine.Simulate(plan);

        var payment = Assert.Single(
            result.Events,
            x => x.Category == "Credit Card Payment");

        Assert.Equal(new DateOnly(2026, 1, 25), payment.Date);

        // The credit-card payment leaves the bank account on 2026-01-25, so the
        // interest-bearing balance drops from that date onwards.
        // 100'000 for 2026-01-01 .. 2026-01-25 (25 days, end-of-previous-day balance)
        // 88'000 for the remaining 340 days.
        var expectedInterest =
            Math.Round(
                (100_000m * 0.01m * 25m / 360m) + (88_000m * 0.01m * 340m / 360m),
                2,
                MidpointRounding.AwayFromZero);

        var interestEvent = Assert.Single(
            result.Events,
            x => x.Category == "Interest");

        Assert.Equal(expectedInterest, interestEvent.Amount);
    }

    private static Account CreateSavingsAccountWithInterest(
        decimal openingBalance,
        DateOnly openingDate,
        decimal annualRatePercent = 1m)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = "Savings",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = openingDate,
            IsActive = true,
            InterestContracts =
            [
                new AccountInterestContract
                {
                    Name = "Savings interest",
                    CalculationMethod = AccountInterestCalculationMethod.FlatBalance,
                    PostingFrequency = InterestPostingFrequency.Yearly,
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
                }
            ]
        };
    }
}
