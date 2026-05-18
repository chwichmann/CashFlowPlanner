using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Analysis;
using CashFlowPlanner.Core.People;

namespace CashFlowPlanner.Core.Tests.Analysis;

public sealed class PersonIncomeSummaryCalculatorTests
{
    [Fact]
    public void CalculateIncomeByPerson_AssignedMonthlyIncome_IsSummedForYear()
    {
        var person = CreatePerson("Christian");
        var account = CreateBankAccount("Salary account");

        var salary = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Salary",
            ToAccountId = account.Id,
            IncomePersonId = person.Id,
            Amount = 5000m,
            Currency = "CHF",
            IsActive = true,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 25),
                Interval = 1
            }
        };

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { account },
            transactions: new List<TransactionDefinition> { salary });

        var result = PersonIncomeSummaryCalculator.CalculateIncomeByPerson(
            plan,
            2026);

        Assert.Equal(60000m, result[person.Id]);
    }

    [Fact]
    public void CalculateIncomeByPerson_AssignedOneTimeIncome_IsSummedForYear()
    {
        var person = CreatePerson("Christian");
        var account = CreateBankAccount("Salary account");

        var bonus = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Bonus",
            ToAccountId = account.Id,
            IncomePersonId = person.Id,
            Amount = 10000m,
            Currency = "CHF",
            IsActive = true,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Once,
                StartDate = new DateOnly(2026, 3, 31),
                Interval = 1
            }
        };

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { account },
            transactions: new List<TransactionDefinition> { bonus });

        var result = PersonIncomeSummaryCalculator.CalculateIncomeByPerson(
            plan,
            2026);

        Assert.Equal(10000m, result[person.Id]);
    }

    [Fact]
    public void CalculateIncomeByPerson_UnassignedIncome_IsIgnored()
    {
        var person = CreatePerson("Christian");
        var account = CreateBankAccount("Salary account");

        var salary = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Unassigned salary",
            ToAccountId = account.Id,
            IncomePersonId = null,
            Amount = 5000m,
            Currency = "CHF",
            IsActive = true,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 25),
                Interval = 1
            }
        };

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { account },
            transactions: new List<TransactionDefinition> { salary });

        var result = PersonIncomeSummaryCalculator.CalculateIncomeByPerson(
            plan,
            2026);

        Assert.Equal(0m, result[person.Id]);
    }

    [Fact]
    public void CalculateIncomeByPerson_InactiveIncome_IsIgnored()
    {
        var person = CreatePerson("Christian");
        var account = CreateBankAccount("Salary account");

        var salary = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Inactive salary",
            ToAccountId = account.Id,
            IncomePersonId = person.Id,
            Amount = 5000m,
            Currency = "CHF",
            IsActive = false,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 25),
                Interval = 1
            }
        };

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { account },
            transactions: new List<TransactionDefinition> { salary });

        var result = PersonIncomeSummaryCalculator.CalculateIncomeByPerson(
            plan,
            2026);

        Assert.Equal(0m, result[person.Id]);
    }

    [Fact]
    public void CalculateIncomeByPerson_TwoPersons_AreCalculatedSeparately()
    {
        var christian = CreatePerson("Christian");
        var partner = CreatePerson("Partner");

        var account = CreateBankAccount("Salary account");

        var christianSalary = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Christian salary",
            ToAccountId = account.Id,
            IncomePersonId = christian.Id,
            Amount = 5000m,
            Currency = "CHF",
            IsActive = true,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 25),
                Interval = 1
            }
        };

        var partnerSalary = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Partner salary",
            ToAccountId = account.Id,
            IncomePersonId = partner.Id,
            Amount = 4000m,
            Currency = "CHF",
            IsActive = true,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 25),
                Interval = 1
            }
        };

        var plan = CreatePlan(
            persons: new List<Person> { christian, partner },
            accounts: new List<Account> { account },
            transactions: new List<TransactionDefinition>
            {
                christianSalary,
                partnerSalary
            });

        var result = PersonIncomeSummaryCalculator.CalculateIncomeByPerson(
            plan,
            2026);

        Assert.Equal(60000m, result[christian.Id]);
        Assert.Equal(48000m, result[partner.Id]);
    }

    [Fact]
    public void CalculateIncomeByPerson_UnknownPersonReference_IsIgnored()
    {
        var person = CreatePerson("Christian");
        var account = CreateBankAccount("Salary account");

        var salary = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Unknown person salary",
            ToAccountId = account.Id,
            IncomePersonId = Guid.NewGuid(),
            Amount = 5000m,
            Currency = "CHF",
            IsActive = true,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 25),
                Interval = 1
            }
        };

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { account },
            transactions: new List<TransactionDefinition> { salary });

        var result = PersonIncomeSummaryCalculator.CalculateIncomeByPerson(
            plan,
            2026);

        Assert.Equal(0m, result[person.Id]);
    }

    private static Person CreatePerson(string name)
    {
        return new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = name
        };
    }

    private static Account CreateBankAccount(string name)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };
    }

    private static CashFlowPlan CreatePlan(
        List<Person> persons,
        List<Account> accounts,
        List<TransactionDefinition> transactions)
    {
        return new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test plan",
            BaseCurrency = "CHF",
            Persons = persons,
            Accounts = accounts,
            Transactions = transactions,
            SimulationSettings = new SimulationSettings
            {
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };
    }
}
