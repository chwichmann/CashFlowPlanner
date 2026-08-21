using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.Core.Tests;

/// <summary>
/// H7: nothing enforced currency anywhere. A USD 1'000 income into a CHF account
/// moved the CHF balance 1'000 -> 2'000, silently, because every balance is a
/// bare decimal and no code path ever compared the two currency strings.
///
/// Two layers now:
///  * CashFlowPlan.Validate() rejects a transaction or Pillar 3a schedule whose
///    currency does not match the account it posts to -- the user-declared case,
///    caught before anything runs;
///  * SimulationEngine's posting path raises a critical CURRENCY_MISMATCH
///    warning for any remaining mismatch -- the net for contract-derived events
///    (mortgage, credit card) whose currency the user does not set per event.
/// </summary>
public sealed class CurrencyEnforcementTests
{
    [Fact]
    public void Validate_TransactionCurrencyDiffersFromTargetAccount_Throws()
    {
        var account = CreateAccount("CHF");

        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Foreign income",
            Kind = TransactionKind.ExternalIncome,
            FromAccountId = null,
            ToAccountId = account.Id,
            Amount = 1_000m,
            Currency = "USD",
            IsActive = true,
            Schedule = TestPlanBuilder.Once(new DateOnly(2026, 2, 1))
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [transaction]);

        var exception = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("USD", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CHF", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_TransactionCurrencyDiffersFromSourceAccount_Throws()
    {
        var account = CreateAccount("CHF");

        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Foreign expense",
            Kind = TransactionKind.ExternalExpense,
            FromAccountId = account.Id,
            ToAccountId = null,
            Amount = 1_000m,
            Currency = "USD",
            IsActive = true,
            Schedule = TestPlanBuilder.Once(new DateOnly(2026, 2, 1))
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [transaction]);

        Assert.Throws<InvalidOperationException>(plan.Validate);
    }

    [Fact]
    public void Validate_InternalTransferBetweenDifferentCurrencies_Throws()
    {
        var chf = CreateAccount("CHF", "CHF account");
        var usd = CreateAccount("USD", "USD account");

        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Cross currency transfer",
            Kind = TransactionKind.InternalTransfer,
            FromAccountId = chf.Id,
            ToAccountId = usd.Id,
            Amount = 500m,
            Currency = "CHF",
            IsActive = true,
            Schedule = TestPlanBuilder.Once(new DateOnly(2026, 2, 1))
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [chf, usd],
            transactions: [transaction]);

        Assert.Throws<InvalidOperationException>(plan.Validate);
    }

    [Fact]
    public void Validate_CurrencyCasingDiffers_IsAccepted()
    {
        var account = CreateAccount("CHF");

        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Income",
            Kind = TransactionKind.ExternalIncome,
            FromAccountId = null,
            ToAccountId = account.Id,
            Amount = 1_000m,
            Currency = "chf",
            IsActive = true,
            Schedule = TestPlanBuilder.Once(new DateOnly(2026, 2, 1))
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [transaction]);

        plan.Validate();
    }

    /// <summary>
    /// The measured symptom of H7: 1'000 USD moved a CHF balance by 1'000.
    /// </summary>
    [Fact]
    public void Simulate_ForeignCurrencyIncome_NoLongerMovesTheBalance()
    {
        var account = CreateAccount("CHF", openingBalance: 1_000m);

        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Foreign income",
            Kind = TransactionKind.ExternalIncome,
            FromAccountId = null,
            ToAccountId = account.Id,
            Amount = 1_000m,
            Currency = "USD",
            IsActive = true,
            Schedule = TestPlanBuilder.Once(new DateOnly(2026, 2, 1))
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [transaction]);

        Assert.Throws<InvalidOperationException>(() => new SimulationEngine().Simulate(plan));
    }

    [Fact]
    public void Validate_Pillar3aScheduleCurrencyDiffersFromPaymentAccount_Throws()
    {
        var account = CreateAccount("CHF");

        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test Person",
            DateOfBirth = new DateOnly(1985, 1, 1)
        };

        var contract = new Pillar3aContract
        {
            Id = Guid.NewGuid(),
            Name = "3a",
            OwnerPersonId = person.Id,
            OpeningDate = new DateOnly(2026, 1, 1),
            Currency = "CHF",
            ContributionSchedules =
            [
                new Pillar3aContributionSchedule
                {
                    Id = Guid.NewGuid(),
                    PaymentAccountId = account.Id,
                    Amount = 7_258m,
                    Currency = "USD",
                    Frequency = ScheduleFrequency.Yearly,
                    StartDate = new DateOnly(2026, 1, 1),
                    Interval = 1,
                    IsActive = true
                }
            ]
        };

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [account],
            pillar3aContracts: [contract]);

        Assert.Throws<InvalidOperationException>(plan.Validate);
    }

    /// <summary>
    /// Mortgage events used to be hard-coded to "CHF" regardless of the plan.
    /// </summary>
    [Fact]
    public void Simulate_MortgageEvents_UseThePlanBaseCurrency()
    {
        var account = CreateAccount("EUR", openingBalance: 500_000m);

        var mortgage = CreateMortgage(account.Id);

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Euro plan",
            BaseCurrency = "EUR",
            Accounts = [account],
            Mortgages = [mortgage],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };

        var result = new SimulationEngine().Simulate(plan);

        var mortgageEvents = result.Events
            .Where(x => x.SourceTransactionId == mortgage.Id)
            .ToList();

        Assert.NotEmpty(mortgageEvents);
        Assert.All(mortgageEvents, x => Assert.Equal("EUR", x.Currency));

        Assert.All(
            result.MortgagePrincipalPoints,
            x => Assert.Equal("EUR", x.Currency));

        Assert.DoesNotContain(result.Warnings, x => x.Code == "CURRENCY_MISMATCH");
    }

    /// <summary>
    /// The posting-path net: a mortgage is implicitly in the plan's base
    /// currency, so billing it to an account in another currency is a mismatch
    /// no per-transaction check can see.
    /// </summary>
    [Fact]
    public void Simulate_MortgageBilledToForeignCurrencyAccount_RaisesCriticalWarning()
    {
        var account = CreateAccount("USD", openingBalance: 500_000m);

        var mortgage = CreateMortgage(account.Id);

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Mixed currency plan",
            BaseCurrency = "CHF",
            Accounts = [account],
            Mortgages = [mortgage],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };

        var result = new SimulationEngine().Simulate(plan);

        var warning = result.Warnings.First(x => x.Code == "CURRENCY_MISMATCH");

        Assert.Equal(WarningSeverity.Critical, warning.Severity);
        Assert.Equal(account.Id, warning.AccountId);
        Assert.Contains("CHF", warning.Message, StringComparison.Ordinal);
        Assert.Contains("USD", warning.Message, StringComparison.Ordinal);
    }

    private static Account CreateAccount(
        string currency,
        string name = "Account",
        decimal openingBalance = 0m)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = AccountType.BankAccount,
            Currency = currency,
            OpeningBalance = openingBalance,
            OpeningDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };
    }

    private static MortgageContract CreateMortgage(Guid paymentAccountId)
    {
        return new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "House",
            Type = MortgageType.Fixed,
            PaymentAccountId = paymentAccountId,
            InitialPrincipal = 500_000m,
            InitialDate = new DateOnly(2026, 1, 1),
            CalculationPrincipal = 500_000m,
            CalculationPrincipalDate = new DateOnly(2026, 1, 1),
            FixedInterestPercent = 1.5m,
            AmortisationMode = AmortisationMode.Direct,
            AnnualAmortisationAmount = 4_000m,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };
    }
}
