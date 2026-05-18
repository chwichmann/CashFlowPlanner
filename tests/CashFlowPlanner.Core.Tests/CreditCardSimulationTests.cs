using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;

namespace CashFlowPlanner.Core.Tests;

public sealed class CreditCardSimulationTests
{
    [Fact]
    public void Simulate_Should_GenerateCreditCardPaymentAndClearDebt()
    {
        // Arrange
        var mainAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Name = "Main Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 10000m,
            OpeningDate = new DateOnly(2026, 6, 1)
        };

        var creditCardAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Name = "Visa",
            Type = AccountType.CreditCard,
            Currency = "CHF",
            OpeningBalance = -1200m,
            OpeningDate = new DateOnly(2026, 6, 1)
        };

        var creditCard = new CreditCardContract
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Name = "Visa",
            CreditCardAccountId = creditCardAccount.Id,
            PaymentAccountId = mainAccount.Id,
            ClosingDayOfMonth = 15,
            PaymentDayOfMonth = 25,
            PaymentMethod = CreditCardPaymentMethod.AutomaticLsv,
            PaymentBusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay,
            StartDate = new DateOnly(2026, 6, 1),
            IsActive = true
        };

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Credit Card Test",
            BaseCurrency = "CHF",
            Persons = [],
            Accounts = [mainAccount, creditCardAccount],
            Transactions = [],
            Mortgages = [],
            CreditCards = [creditCard],
            Pillar3aContracts = [],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 30)
            }
        };

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var payment = result.Events.Single(x => x.SourceTransactionId == creditCard.Id);

        Assert.Equal(new DateOnly(2026, 6, 25), payment.Date);
        Assert.Equal(1200m, payment.Amount);

        Assert.Equal(8800m, result.GetBalance(mainAccount.Id, new DateOnly(2026, 6, 30)));
        Assert.Equal(0m, result.GetBalance(creditCardAccount.Id, new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void Simulate_Should_NotPayOpeningCreditCardDebt_EveryMonth()
    {
        // Arrange
        var mainAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Name = "Main Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 10000m,
            OpeningDate = new DateOnly(2026, 6, 1)
        };

        var creditCardAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Name = "Visa",
            Type = AccountType.CreditCard,
            Currency = "CHF",
            OpeningBalance = -1200m,
            OpeningDate = new DateOnly(2026, 6, 1)
        };

        var creditCard = new CreditCardContract
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Name = "Visa",
            CreditCardAccountId = creditCardAccount.Id,
            PaymentAccountId = mainAccount.Id,
            ClosingDayOfMonth = 15,
            PaymentDayOfMonth = 25,
            PaymentMethod = CreditCardPaymentMethod.AutomaticLsv,
            PaymentBusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay,
            StartDate = new DateOnly(2026, 6, 1),
            IsActive = true
        };

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Credit Card Test",
            BaseCurrency = "CHF",
            Persons = [],
            Accounts = [mainAccount, creditCardAccount],
            Transactions = [],
            Mortgages = [],
            CreditCards = [creditCard],
            Pillar3aContracts = [],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 8, 31)
            }
        };

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var payments = result.Events
            .Where(x => x.SourceTransactionId == creditCard.Id)
            .ToList();

        var payment = Assert.Single(payments);

        Assert.Equal(1200m, payment.Amount);

        Assert.Equal(8800m, result.GetBalance(mainAccount.Id, new DateOnly(2026, 8, 31)));
        Assert.Equal(0m, result.GetBalance(creditCardAccount.Id, new DateOnly(2026, 8, 31)));
    }
}