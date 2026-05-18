using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.Core.Tests;

public sealed class MortgageSimulationTests
{
    [Fact]
    public void Simulate_Should_ApplyMortgageInterestAndDirectAmortisation()
    {
        // Arrange
        var savingsAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Name = "Savings Account",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = 100000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var mortgageAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Name = "House Mortgage",
            Type = AccountType.Mortgage,
            Currency = "CHF",
            OpeningBalance = -705000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var mortgage = new MortgageContract
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Name = "SARON Mortgage",
            Type = MortgageType.Saron,
            PaymentAccountId = savingsAccount.Id,
            InitialPrincipal = 750000m,
            InitialDate = new DateOnly(2021, 8, 1),
            CalculationPrincipal = 705000m,
            CalculationPrincipalDate = new DateOnly(2026, 1, 1),
            FixedInterestPercent = 0.65m,
            SaronRates =
            [
                new MortgageInterestRatePoint
                {
                    Date = new DateOnly(2026, 1, 1),
                    RatePercent = 1.20m
                }
            ],
            AmortisationMode = AmortisationMode.Direct,
            AnnualAmortisationAmount = 9000m,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Mortgage Test Plan",
            BaseCurrency = "CHF",
            Persons = [],
            Accounts = [savingsAccount, mortgageAccount],
            Transactions = [],
            Mortgages = [mortgage],
            CreditCards = [],
            Pillar3aContracts = [],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 3, 31)
            }
        };

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var events = result.Events
            .Where(x => x.SourceTransactionId == mortgage.Id)
            .ToList();

        Assert.Equal(2, events.Count);

        var interest = events.Single(x => x.Name.EndsWith("interest"));
        var amortisation = events.Single(x => x.Name.EndsWith("amortisation"));

        var expectedSavingsBalance =
            100000m
            - interest.Amount
            - amortisation.Amount;

        Assert.Equal(
            expectedSavingsBalance,
            result.GetBalance(savingsAccount.Id, new DateOnly(2026, 3, 31)));

        Assert.Equal(
            702750m,
            result.GetMortgagePrincipal(mortgage.Id, new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void Simulate_Should_ApplyIndirectAmortisation_WithoutReducingMortgage()
    {
        // Arrange
        var savingsAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Name = "Savings Account",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = 100000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var mortgageAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Name = "House Mortgage",
            Type = AccountType.Mortgage,
            Currency = "CHF",
            OpeningBalance = -705000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var pillar3aAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
            Name = "Pillar 3a",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = 50000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var mortgage = new MortgageContract
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Name = "SARON Mortgage",
            Type = MortgageType.Saron,
            PaymentAccountId = savingsAccount.Id,
            IndirectAmortisationAccountId = pillar3aAccount.Id,
            InitialPrincipal = 750000m,
            InitialDate = new DateOnly(2021, 8, 1),
            CalculationPrincipal = 705000m,
            CalculationPrincipalDate = new DateOnly(2026, 1, 1),
            FixedInterestPercent = 0.65m,
            SaronRates =
            [
                new MortgageInterestRatePoint
                {
                    Date = new DateOnly(2026, 1, 1),
                    RatePercent = 1.20m
                }
            ],
            AmortisationMode = AmortisationMode.Indirect,
            AnnualAmortisationAmount = 9000m,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Mortgage Test Plan",
            BaseCurrency = "CHF",
            Persons = [],
            Accounts = [savingsAccount, mortgageAccount, pillar3aAccount],
            Transactions = [],
            Mortgages = [mortgage],
            CreditCards = [],
            Pillar3aContracts = [],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 3, 31)
            }
        };

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var events = result.Events
            .Where(x => x.SourceTransactionId == mortgage.Id)
            .ToList();

        Assert.Equal(2, events.Count);

        var interest = events.Single(x => x.Name.EndsWith("interest"));
        var indirectAmortisation = events.Single(x => x.Name.EndsWith("indirect amortisation"));

        Assert.Equal(2250m, indirectAmortisation.Amount);

        Assert.Equal(
            100000m - interest.Amount - indirectAmortisation.Amount,
            result.GetBalance(savingsAccount.Id, new DateOnly(2026, 3, 31)));

        Assert.Equal(
            705000m,
            result.GetMortgagePrincipal(mortgage.Id, new DateOnly(2026, 3, 31)));

        Assert.Equal(
            52250m,
            result.GetBalance(pillar3aAccount.Id, new DateOnly(2026, 3, 31)));
    }
}