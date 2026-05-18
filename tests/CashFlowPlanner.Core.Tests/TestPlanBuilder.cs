using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

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