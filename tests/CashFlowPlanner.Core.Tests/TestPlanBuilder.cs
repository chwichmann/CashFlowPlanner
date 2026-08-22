using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Core.Tests;

public static class TestPlanBuilder
{
    public static CashFlowPlan CreatePlan(
        List<Person>? persons = null,
        List<Account>? accounts = null,
        List<TransactionDefinition>? transactions = null,
        List<MortgageContract>? mortgages = null,
        List<CreditCardContract>? creditCards = null,
        List<Pillar3aContract>? pillar3aContracts = null,
        List<RealEstateAsset>? realEstateAssets = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        return new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test plan",
            BaseCurrency = "CHF",

            Persons = persons ?? [],
            Accounts = accounts ?? [],
            Transactions = transactions ?? [],
            Mortgages = mortgages ?? [],
            CreditCards = creditCards ?? [],
            Pillar3aContracts = pillar3aContracts ?? [],
            RealEstateAssets = realEstateAssets ?? [],

            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = startDate ?? new DateOnly(2026, 1, 1),
                EndDate = endDate ?? new DateOnly(2026, 12, 31),
                Granularity = SimulationGranularity.Daily,
                IncludeInactiveAccounts = false,
                WarnOnNegativeBankBalance = true
            }
        };
    }

    public static Account CreateBankAccount(
        decimal openingBalance = 0m,
        DateOnly? openingDate = null,
        string name = "Bank Account")
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = openingDate ?? new DateOnly(2026, 1, 1),
            IsActive = true
        };
    }

    public static Account CreateSavingsAccount(
        decimal openingBalance = 0m,
        DateOnly? openingDate = null,
        string name = "Savings Account")
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = openingDate ?? new DateOnly(2026, 1, 1),
            IsActive = true
        };
    }

    /// <summary>
    /// A Pillar 3a account that passes <see cref="Validation.AccountValidator"/>:
    /// it needs exactly one owner and a subtype, so a plan-level test has to
    /// supply the person who owns it.
    /// </summary>
    public static Account CreatePillar3aAccount(
        decimal openingBalance = 0m,
        DateOnly? openingDate = null,
        string name = "Pillar 3a Account",
        Guid? ownerPersonId = null)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = AccountType.Pillar3a,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = openingDate ?? new DateOnly(2026, 1, 1),
            IsActive = true,
            Pillar3aSubtype = Pillar3aAccountSubtype.FundSolution,
            Owners = ownerPersonId is null
                ? []
                : [new AccountOwner { PersonId = ownerPersonId.Value, OwnershipShare = 1m }]
        };
    }

    public static Account CreateCreditCardAccount(
        decimal openingBalance = 0m,
        DateOnly? openingDate = null,
        string name = "Credit Card")
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = AccountType.CreditCard,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = openingDate ?? new DateOnly(2026, 1, 1),
            IsActive = true
        };
    }

    public static TransactionDefinition ExternalIncome(
        Guid toAccountId,
        decimal amount,
        Schedule schedule,
        string name = "Income",
        int priority = 100)
    {
        return new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = TransactionKind.ExternalIncome,
            FromAccountId = null,
            ToAccountId = toAccountId,
            Amount = amount,
            Currency = "CHF",
            Priority = priority,
            IsActive = true,
            Schedule = schedule
        };
    }

    public static TransactionDefinition ExternalExpense(
        Guid fromAccountId,
        decimal amount,
        Schedule schedule,
        string name = "Expense",
        int priority = 100)
    {
        return new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = TransactionKind.ExternalExpense,
            FromAccountId = fromAccountId,
            ToAccountId = null,
            Amount = amount,
            Currency = "CHF",
            Priority = priority,
            IsActive = true,
            Schedule = schedule
        };
    }

    public static TransactionDefinition InternalTransfer(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        Schedule schedule,
        string name = "Transfer",
        int priority = 100)
    {
        return new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = TransactionKind.InternalTransfer,
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
            Currency = "CHF",
            Priority = priority,
            IsActive = true,
            Schedule = schedule
        };
    }

    public static TransactionDefinition DebtIncrease(
        Guid debtAccountId,
        decimal amount,
        Schedule schedule,
        string name = "Debt increase",
        int priority = 100)
    {
        return new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = TransactionKind.DebtIncrease,
            FromAccountId = null,
            ToAccountId = debtAccountId,
            Amount = amount,
            Currency = "CHF",
            Priority = priority,
            IsActive = true,
            Schedule = schedule
        };
    }

    public static TransactionDefinition DebtPayment(
        Guid fromAccountId,
        Guid debtAccountId,
        decimal amount,
        Schedule schedule,
        string name = "Debt payment",
        int priority = 100)
    {
        return new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = TransactionKind.DebtPayment,
            FromAccountId = fromAccountId,
            ToAccountId = debtAccountId,
            Amount = amount,
            Currency = "CHF",
            Priority = priority,
            IsActive = true,
            Schedule = schedule
        };
    }

    public static Schedule Once(DateOnly date)
    {
        return new Schedule
        {
            Frequency = ScheduleFrequency.Once,
            StartDate = date,
            Interval = 1
        };
    }

    public static Schedule Monthly(
        DateOnly startDate,
        int? dayOfMonth = null,
        DateOnly? endDate = null,
        BusinessDayAdjustment businessDayAdjustment = BusinessDayAdjustment.None)
    {
        return new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = startDate,
            EndDate = endDate,
            Interval = 1,
            DayOfMonth = dayOfMonth,
            BusinessDayAdjustment = businessDayAdjustment
        };
    }
}