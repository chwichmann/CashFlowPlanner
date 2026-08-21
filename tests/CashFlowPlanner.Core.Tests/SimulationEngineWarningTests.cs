using System.Globalization;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;

namespace CashFlowPlanner.Core.Tests;

public sealed class SimulationEngineWarningTests
{
    /// <summary>
    /// NEGATIVE_BALANCE used to be raised once per account per DAY -- 7'305
    /// warnings for one overdrawn account over 20 years, which buried every
    /// critical in the same list. One warning per contiguous episode instead,
    /// carrying the span and the low point.
    /// </summary>
    [Fact]
    public void Simulate_OverdrawnTwice_RaisesOneWarningPerEpisode()
    {
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 100m,
            openingDate: new DateOnly(2026, 1, 1));

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions:
            [
                // Episode 1: 10.01 .. 19.01, low point -400 on 15.01.
                TestPlanBuilder.ExternalExpense(
                    account.Id,
                    300m,
                    TestPlanBuilder.Once(new DateOnly(2026, 1, 10)),
                    name: "Out 1"),

                TestPlanBuilder.ExternalExpense(
                    account.Id,
                    200m,
                    TestPlanBuilder.Once(new DateOnly(2026, 1, 15)),
                    name: "Out 2"),

                TestPlanBuilder.ExternalIncome(
                    account.Id,
                    400m,
                    TestPlanBuilder.Once(new DateOnly(2026, 1, 20)),
                    name: "In 1"),

                // Episode 2: 01.03 to the end of the simulated range.
                TestPlanBuilder.ExternalExpense(
                    account.Id,
                    500m,
                    TestPlanBuilder.Once(new DateOnly(2026, 3, 1)),
                    name: "Out 3")
            ],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 3, 31));

        var result = new SimulationEngine().Simulate(plan);

        var negativeWarnings = result.Warnings
            .Where(x => x.Code == "NEGATIVE_BALANCE")
            .OrderBy(x => x.Date)
            .ToList();

        Assert.Equal(2, negativeWarnings.Count);

        Assert.Equal(new DateOnly(2026, 1, 10), negativeWarnings[0].Date);
        Assert.Contains("2026-01-19", negativeWarnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("2026-01-15", negativeWarnings[0].Message, StringComparison.Ordinal);

        // The message formats money with the current culture, exactly as the
        // engine does, so this assertion does not depend on the machine locale.
        Assert.Contains(
            (-400m).ToString("N2", CultureInfo.CurrentCulture),
            negativeWarnings[0].Message,
            StringComparison.Ordinal);

        Assert.Equal(new DateOnly(2026, 3, 1), negativeWarnings[1].Date);
        Assert.Contains("2026-03-31", negativeWarnings[1].Message, StringComparison.Ordinal);

        Assert.All(negativeWarnings, x => Assert.Equal(account.Id, x.AccountId));
    }

    [Fact]
    public void Simulate_PermanentlyOverdrawn_RaisesExactlyOneWarning()
    {
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: -1_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2028, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        var warning = Assert.Single(
            result.Warnings,
            x => x.Code == "NEGATIVE_BALANCE");

        Assert.Equal(new DateOnly(2026, 1, 1), warning.Date);
        Assert.Contains("1096 day(s)", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The credit-card generator was the only one handed plan.Accounts unfiltered,
    /// so a contract on an inactive card account still produced payment events.
    /// The engine had no balance for that card, so each one raised a spurious
    /// UNKNOWN_ACCOUNT critical -- and the other leg still debited the bank
    /// account for real.
    /// </summary>
    [Fact]
    public void Simulate_CreditCardOnInactiveAccount_IsSkippedInsteadOfWarning()
    {
        var bank = TestPlanBuilder.CreateBankAccount(
            openingBalance: 10_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var card = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Retired card",
            Type = AccountType.CreditCard,
            Currency = "CHF",
            OpeningBalance = -1_500m,
            OpeningDate = new DateOnly(2026, 1, 1),
            IsActive = false
        };

        var creditCard = new CreditCardContract
        {
            Id = Guid.NewGuid(),
            Name = "Retired card contract",
            CreditCardAccountId = card.Id,
            PaymentAccountId = bank.Id,
            ClosingDayOfMonth = 20,
            PaymentDayOfMonth = 8,
            PaymentBusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay,
            StartDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [bank, card],
            creditCards: [creditCard],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        Assert.DoesNotContain(result.Warnings, x => x.Code == "UNKNOWN_ACCOUNT");
        Assert.DoesNotContain(result.Events, x => x.SourceTransactionId == creditCard.Id);

        // The bank account must not have been debited for a card nobody tracks.
        Assert.Equal(10_000m, result.GetBalance(bank.Id, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Simulate_CreditCardOnActiveAccount_StillGeneratesPayments()
    {
        var bank = TestPlanBuilder.CreateBankAccount(
            openingBalance: 10_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var card = TestPlanBuilder.CreateCreditCardAccount(
            openingBalance: -1_500m,
            openingDate: new DateOnly(2026, 1, 1));

        var creditCard = new CreditCardContract
        {
            Id = Guid.NewGuid(),
            Name = "Active card contract",
            CreditCardAccountId = card.Id,
            PaymentAccountId = bank.Id,
            ClosingDayOfMonth = 20,
            PaymentDayOfMonth = 8,
            PaymentBusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay,
            StartDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [bank, card],
            creditCards: [creditCard],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        Assert.Contains(result.Events, x => x.SourceTransactionId == creditCard.Id);
        Assert.DoesNotContain(result.Warnings, x => x.Code == "UNKNOWN_ACCOUNT");
    }
}
