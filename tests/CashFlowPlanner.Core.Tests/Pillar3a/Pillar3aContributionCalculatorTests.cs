using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.Core.Tests.Pillar3a;

public sealed class Pillar3aContributionCalculatorTests
{
    [Fact]
    public void Calculate_ContributionToPillar3aAccount_IsAssignedToAccountOwner()
    {
        var person = CreatePerson("Christian");

        var sourceAccount = CreateBankAccount("Private account");
        var pillar3aAccount = CreatePillar3aAccount("3a VIAC", person.Id);

        var contribution = CreateMonthlyContribution(
            sourceAccount.Id,
            pillar3aAccount.Id,
            600m);

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { sourceAccount, pillar3aAccount },
            transactions: new List<TransactionDefinition> { contribution });

        var result = Pillar3aContributionCalculator.Calculate(
            plan,
            2026,
            Create2026LimitRule());

        var summary = Assert.Single(result);

        Assert.Equal(person.Id, summary.PersonId);
        Assert.Equal(2026, summary.Year);
        Assert.Equal(7258m, summary.MaxAllowed);
        Assert.Equal(7200m, summary.Contributions);
        Assert.Equal(58m, summary.Remaining);
        Assert.Equal(0m, summary.Excess);
        Assert.False(summary.IsExceeded);
    }

    [Fact]
    public void Calculate_ContributionAboveLimit_ReturnsExcess()
    {
        var person = CreatePerson("Christian");

        var sourceAccount = CreateBankAccount("Private account");
        var pillar3aAccount = CreatePillar3aAccount("3a VIAC", person.Id);

        var contribution = CreateOneTimeContribution(
            sourceAccount.Id,
            pillar3aAccount.Id,
            8000m);

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { sourceAccount, pillar3aAccount },
            transactions: new List<TransactionDefinition> { contribution });

        var result = Pillar3aContributionCalculator.Calculate(
            plan,
            2026,
            Create2026LimitRule());

        var summary = Assert.Single(result);

        Assert.Equal(7258m, summary.MaxAllowed);
        Assert.Equal(8000m, summary.Contributions);
        Assert.Equal(0m, summary.Remaining);
        Assert.Equal(742m, summary.Excess);
        Assert.True(summary.IsExceeded);
    }

    [Fact]
    public void Calculate_MultiplePillar3aAccountsForSamePerson_AreCombined()
    {
        var person = CreatePerson("Christian");

        var sourceAccount = CreateBankAccount("Private account");
        var pillar3aAccount1 = CreatePillar3aAccount("3a VIAC 1", person.Id);
        var pillar3aAccount2 = CreatePillar3aAccount("3a VIAC 2", person.Id);

        var contribution1 = CreateOneTimeContribution(
            sourceAccount.Id,
            pillar3aAccount1.Id,
            4000m);

        var contribution2 = CreateOneTimeContribution(
            sourceAccount.Id,
            pillar3aAccount2.Id,
            3258m);

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account>
            {
                sourceAccount,
                pillar3aAccount1,
                pillar3aAccount2
            },
            transactions: new List<TransactionDefinition>
            {
                contribution1,
                contribution2
            });

        var result = Pillar3aContributionCalculator.Calculate(
            plan,
            2026,
            Create2026LimitRule());

        var summary = Assert.Single(result);

        Assert.Equal(7258m, summary.MaxAllowed);
        Assert.Equal(7258m, summary.Contributions);
        Assert.Equal(0m, summary.Remaining);
        Assert.Equal(0m, summary.Excess);
        Assert.False(summary.IsExceeded);
    }

    [Fact]
    public void Calculate_TwoPersons_AreSeparatedByPillar3aAccountOwner()
    {
        var christian = CreatePerson("Christian");
        var partner = CreatePerson("Partner");

        var sourceAccount = CreateBankAccount("Private account");
        var christian3a = CreatePillar3aAccount("Christian 3a", christian.Id);
        var partner3a = CreatePillar3aAccount("Partner 3a", partner.Id);

        var christianContribution = CreateOneTimeContribution(
            sourceAccount.Id,
            christian3a.Id,
            7000m);

        var partnerContribution = CreateOneTimeContribution(
            sourceAccount.Id,
            partner3a.Id,
            5000m);

        var plan = CreatePlan(
            persons: new List<Person> { christian, partner },
            accounts: new List<Account>
            {
                sourceAccount,
                christian3a,
                partner3a
            },
            transactions: new List<TransactionDefinition>
            {
                christianContribution,
                partnerContribution
            });

        var result = Pillar3aContributionCalculator.Calculate(
            plan,
            2026,
            Create2026LimitRule());

        var christianSummary = result.Single(x => x.PersonId == christian.Id);
        var partnerSummary = result.Single(x => x.PersonId == partner.Id);

        Assert.Equal(7000m, christianSummary.Contributions);
        Assert.Equal(258m, christianSummary.Remaining);
        Assert.Equal(0m, christianSummary.Excess);

        Assert.Equal(5000m, partnerSummary.Contributions);
        Assert.Equal(2258m, partnerSummary.Remaining);
        Assert.Equal(0m, partnerSummary.Excess);
    }

    [Fact]
    public void Calculate_ContributionToNormalBankAccount_IsIgnored()
    {
        var person = CreatePerson("Christian");

        var sourceAccount = CreateBankAccount("Private account");
        var normalSavingsAccount = CreateBankAccount("Normal savings");

        var transfer = CreateOneTimeContribution(
            sourceAccount.Id,
            normalSavingsAccount.Id,
            7258m);

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { sourceAccount, normalSavingsAccount },
            transactions: new List<TransactionDefinition> { transfer });

        var result = Pillar3aContributionCalculator.Calculate(
            plan,
            2026,
            Create2026LimitRule());

        var summary = Assert.Single(result);

        Assert.Equal(0m, summary.Contributions);
        Assert.Equal(7258m, summary.Remaining);
        Assert.Equal(0m, summary.Excess);
    }

    [Fact]
    public void Calculate_InactiveContribution_IsIgnored()
    {
        var person = CreatePerson("Christian");

        var sourceAccount = CreateBankAccount("Private account");
        var pillar3aAccount = CreatePillar3aAccount("3a VIAC", person.Id);

        var contribution = CreateOneTimeContribution(
            sourceAccount.Id,
            pillar3aAccount.Id,
            7258m,
            isActive: false);

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { sourceAccount, pillar3aAccount },
            transactions: new List<TransactionDefinition> { contribution });

        var result = Pillar3aContributionCalculator.Calculate(
            plan,
            2026,
            Create2026LimitRule());

        var summary = Assert.Single(result);

        Assert.Equal(0m, summary.Contributions);
        Assert.Equal(7258m, summary.Remaining);
        Assert.Equal(0m, summary.Excess);
    }

    [Fact]
    public void Calculate_PersonIncome_DoesNotAffectPillar3aLimit()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Christian",
            AnnualEarnedIncome = 250000m
        };

        var sourceAccount = CreateBankAccount("Private account");
        var pillar3aAccount = CreatePillar3aAccount("3a VIAC", person.Id);

        var contribution = CreateOneTimeContribution(
            sourceAccount.Id,
            pillar3aAccount.Id,
            7000m);

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account> { sourceAccount, pillar3aAccount },
            transactions: new List<TransactionDefinition> { contribution });

        var result = Pillar3aContributionCalculator.Calculate(
            plan,
            2026,
            Create2026LimitRule());

        var summary = Assert.Single(result);

        Assert.Equal(7258m, summary.MaxAllowed);
        Assert.Equal(7000m, summary.Contributions);
        Assert.Equal(258m, summary.Remaining);
        Assert.Equal(0m, summary.Excess);
    }

    [Fact]
    public void Calculate_LimitRuleForDifferentYear_Throws()
    {
        var person = CreatePerson("Christian");

        var plan = CreatePlan(
            persons: new List<Person> { person },
            accounts: new List<Account>(),
            transactions: new List<TransactionDefinition>());

        var limitRule = new Pillar3aLimitRule
        {
            Year = 2025,
            MaxContributionPerPerson = 7258m
        };

        Assert.Throws<InvalidOperationException>(() =>
            Pillar3aContributionCalculator.Calculate(
                plan,
                2026,
                limitRule));
    }

    [Fact]
    public void Simulate_Should_GeneratePillar3aContributionEvents()
    {
        var bankAccount = TestPlanBuilder.CreateBankAccount(
            openingBalance: 10_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Christian"
        };

        var pillar3a = new Pillar3aContract
        {
            Name = "VIAC",
            OwnerPersonId = person.Id,
            Type = Pillar3aContractType.Investment,
            OpeningValue = 0m,
            OpeningDate = new DateOnly(2026, 1, 1),
            ContributionSchedules =
            [
                new Pillar3aContributionSchedule
            {
                PaymentAccountId = bankAccount.Id,
                StartDate = new DateOnly(2026, 1, 1),
                Amount = 100m,
                Frequency = ScheduleFrequency.Monthly,
                DayOfMonth = 10
            }
            ]
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [bankAccount],
            persons: [person],
            transactions: [],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 3, 31));

        plan.Pillar3aContracts.Add(pillar3a);

        var engine = new SimulationEngine();

        var result = engine.Simulate(plan);

        var contributionEvents = result.Events
            .Where(x => x.Category == "Pillar 3a Contribution")
            .ToList();

        Assert.Equal(3, contributionEvents.Count);

        var endBalance = result.GetBalance(
            bankAccount.Id,
            new DateOnly(2026, 3, 31));

        Assert.Equal(10_000m - 300m, endBalance);

        Assert.All(contributionEvents, cashFlowEvent =>
        {
            Assert.Equal(TransactionKind.ExternalExpense, cashFlowEvent.Kind);
            Assert.Equal(bankAccount.Id, cashFlowEvent.FromAccountId);
            Assert.Null(cashFlowEvent.ToAccountId);
            Assert.Equal(100m, cashFlowEvent.Amount);
            Assert.Equal("CHF", cashFlowEvent.Currency);
            Assert.Equal("Pillar 3a Contribution", cashFlowEvent.Category);
            Assert.Equal("Generated from Pillar 3a contract.", cashFlowEvent.Notes);
            Assert.Equal(pillar3a.Id, cashFlowEvent.SourceTransactionId);
        });

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

    private static Account CreatePillar3aAccount(
        string name,
        Guid ownerPersonId)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = AccountType.Pillar3a,
            Pillar3aSubtype = Pillar3aAccountSubtype.FundSolution,
            Currency = "CHF",
            OpeningDate = new DateOnly(2026, 1, 1),
            IsActive = true,
            Owners = new List<AccountOwner>
            {
                new AccountOwner
                {
                    PersonId = ownerPersonId,
                    OwnershipShare = 1m
                }
            }
        };
    }

    private static TransactionDefinition CreateOneTimeContribution(
    Guid fromAccountId,
    Guid toAccountId,
    decimal amount,
    bool isActive = true)
    {
        return new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Pillar 3a contribution",
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
            Currency = "CHF",
            IsActive = isActive,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Once,
                StartDate = new DateOnly(2026, 6, 1),
                Interval = 1
            }
        };
    }

    private static TransactionDefinition CreateMonthlyContribution(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount)
    {
        return new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Monthly Pillar 3a contribution",
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
            Currency = "CHF",
            IsActive = true,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 1),
                Interval = 1
            }
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

    private static Pillar3aLimitRule Create2026LimitRule()
    {
        return new Pillar3aLimitRule
        {
            Year = 2026,
            MaxContributionPerPerson = 7258m
        };
    }
}