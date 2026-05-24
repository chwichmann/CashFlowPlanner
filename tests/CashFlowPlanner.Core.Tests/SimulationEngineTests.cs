using CashFlowPlanner.Core;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.Core.Tests;

public sealed class SimulationEngineTests
{
    [Fact]
    public void Simulate_Should_KeepOpeningBalance_WhenNoTransactionsExist()
    {
        // Arrange
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 1000m,
            openingDate: new DateOnly(2026, 6, 1));

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var endBalance = result.GetBalance(account.Id, new DateOnly(2026, 6, 30));
        Assert.Equal(1000m, endBalance);
    }

    [Fact]
    public void Simulate_Should_IncreaseBalance_ForExternalIncome()
    {
        // Arrange
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 1000m,
            openingDate: new DateOnly(2026, 6, 1));

        var salary = TestPlanBuilder.ExternalIncome(
            toAccountId: account.Id,
            amount: 5000m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 25)),
            name: "Salary");

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [salary],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var balanceBeforeSalary = result.GetBalance(account.Id, new DateOnly(2026, 6, 24));
        var balanceAfterSalary = result.GetBalance(account.Id, new DateOnly(2026, 6, 25));
        var endBalance = result.GetBalance(account.Id, new DateOnly(2026, 6, 30));

        Assert.Equal(1000m, balanceBeforeSalary);
        Assert.Equal(6000m, balanceAfterSalary);
        Assert.Equal(6000m, endBalance);
    }

    [Fact]
    public void Simulate_Should_DecreaseBalance_ForExternalExpense()
    {
        // Arrange
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 3000m,
            openingDate: new DateOnly(2026, 6, 1));

        var rent = TestPlanBuilder.ExternalExpense(
            fromAccountId: account.Id,
            amount: 2200m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 1)),
            name: "Rent");

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [rent],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var endBalance = result.GetBalance(account.Id, new DateOnly(2026, 6, 30));
        Assert.Equal(800m, endBalance);
    }

    [Fact]
    public void Simulate_Should_MoveMoney_ForInternalTransfer()
    {
        // Arrange
        var mainAccount = TestPlanBuilder.CreateBankAccount(
            openingBalance: 5000m,
            openingDate: new DateOnly(2026, 6, 1));

        var savingsAccount = TestPlanBuilder.CreateSavingsAccount(
            openingBalance: 10000m,
            openingDate: new DateOnly(2026, 6, 1));

        var transfer = TestPlanBuilder.InternalTransfer(
            fromAccountId: mainAccount.Id,
            toAccountId: savingsAccount.Id,
            amount: 1000m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 10)),
            name: "Savings Transfer");

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [mainAccount, savingsAccount],
            transactions: [transfer],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var mainEndBalance = result.GetBalance(mainAccount.Id, new DateOnly(2026, 6, 30));
        var savingsEndBalance = result.GetBalance(savingsAccount.Id, new DateOnly(2026, 6, 30));

        Assert.Equal(4000m, mainEndBalance);
        Assert.Equal(11000m, savingsEndBalance);
    }

    [Fact]
    public void Simulate_Should_DecreaseDebtAccount_ForDebtIncrease()
    {
        // Arrange
        var creditCard = TestPlanBuilder.CreateCreditCardAccount(
            openingBalance: 0m,
            openingDate: new DateOnly(2026, 6, 1));

        var purchase = TestPlanBuilder.DebtIncrease(
            debtAccountId: creditCard.Id,
            amount: 250m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 10)),
            name: "Credit Card Purchase");

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [creditCard],
            transactions: [purchase],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var cardBalance = result.GetBalance(creditCard.Id, new DateOnly(2026, 6, 30));
        Assert.Equal(-250m, cardBalance);
    }

    [Fact]
    public void Simulate_Should_ReduceDebtAccountAndBankBalance_ForDebtPayment()
    {
        // Arrange
        var mainAccount = TestPlanBuilder.CreateBankAccount(
            openingBalance: 3000m,
            openingDate: new DateOnly(2026, 6, 1));

        var creditCard = TestPlanBuilder.CreateCreditCardAccount(
            openingBalance: -1200m,
            openingDate: new DateOnly(2026, 6, 1));

        var lsvPayment = TestPlanBuilder.DebtPayment(
            fromAccountId: mainAccount.Id,
            debtAccountId: creditCard.Id,
            amount: 1200m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 5)),
            name: "Credit Card LSV");

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [mainAccount, creditCard],
            transactions: [lsvPayment],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        var mainEndBalance = result.GetBalance(mainAccount.Id, new DateOnly(2026, 6, 30));
        var cardEndBalance = result.GetBalance(creditCard.Id, new DateOnly(2026, 6, 30));

        Assert.Equal(1800m, mainEndBalance);
        Assert.Equal(0m, cardEndBalance);
    }

    [Fact]
    public void Simulate_Should_CreateWarning_WhenBankBalanceGetsNegative()
    {
        // Arrange
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 1000m,
            openingDate: new DateOnly(2026, 6, 1));

        var rent = TestPlanBuilder.ExternalExpense(
            fromAccountId: account.Id,
            amount: 2200m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 1)),
            name: "Rent");

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [rent],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        Assert.Contains(result.Warnings, warning =>
            warning.Code == "NEGATIVE_BALANCE" &&
            warning.AccountId == account.Id);
    }

    [Fact]
    public void Simulate_Should_ApplyLowerPriorityFirst_OnSameDate()
    {
        // Arrange
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 0m,
            openingDate: new DateOnly(2026, 6, 1));

        var salary = TestPlanBuilder.ExternalIncome(
            toAccountId: account.Id,
            amount: 5000m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 1)),
            name: "Salary",
            priority: 10);

        var rent = TestPlanBuilder.ExternalExpense(
            fromAccountId: account.Id,
            amount: 2200m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 1)),
            name: "Rent",
            priority: 100);

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [rent, salary],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 1));

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        Assert.Equal("Salary", result.Events[0].Name);
        Assert.Equal("Rent", result.Events[1].Name);

        var endBalance = result.GetBalance(account.Id, new DateOnly(2026, 6, 1));
        Assert.Equal(2800m, endBalance);
    }

    [Fact]
    public void GetEffectiveDateRange_RollingOneYearFromFirstDayOfMonth_ReturnsExpectedRange()
    {
        var settings = new SimulationSettings
        {
            DateMode = SimulationDateMode.RollingHorizon,
            StartAnchor = SimulationStartAnchor.FirstDayOfCurrentMonth,
            HorizonMonths = 12
        };

        var range = settings.GetEffectiveDateRange(new DateOnly(2026, 5, 15));

        Assert.Equal(new DateOnly(2026, 5, 1), range.StartDate);
        Assert.Equal(new DateOnly(2027, 4, 30), range.EndDate);
    }

    [Fact]
    public void GetEffectiveDateRange_ExplicitDateRange_ReturnsConfiguredRange()
    {
        var settings = new SimulationSettings
        {
            DateMode = SimulationDateMode.ExplicitDateRange,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 30)
        };

        var range = settings.GetEffectiveDateRange(new DateOnly(2026, 5, 15));

        Assert.Equal(new DateOnly(2026, 6, 1), range.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 30), range.EndDate);
    }

    [Fact]
    public void Simulate_WhenAccountOpeningDateIsInsideSimulationRange_AppliesOpeningBalanceOnOpeningDate()
    {
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 748.60m,
            openingDate: new DateOnly(2026, 5, 15));

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [],
            startDate: new DateOnly(2026, 5, 11),
            endDate: new DateOnly(2026, 5, 31));

        var engine = new SimulationEngine();

        var result = engine.Simulate(plan);

        Assert.Equal(0m, result.GetBalance(account.Id, new DateOnly(2026, 5, 14)));
        Assert.Equal(748.60m, result.GetBalance(account.Id, new DateOnly(2026, 5, 15)));
        Assert.Equal(748.60m, result.GetBalance(account.Id, new DateOnly(2026, 5, 31)));
    }

    [Fact]
    public void Simulate_Should_GeneratePillar3aContributionEvents_AndReducePaymentAccountBalance()
    {
        var bankAccount = TestPlanBuilder.CreateBankAccount(
            openingBalance: 10_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Christian",
            RetirementDate = new DateOnly(2050, 1, 1)
        };

        var pillar3a = new Pillar3aContract
        {
            Id = Guid.NewGuid(),
            Name = "VIAC",
            OwnerPersonId = person.Id,
            Type = Pillar3aContractType.Investment,
            OpeningValue = 0m,
            OpeningDate = new DateOnly(2026, 1, 1),
            Currency = "CHF",
            ContributionSchedules =
            [
                new Pillar3aContributionSchedule
            {
                Id = Guid.NewGuid(),
                PaymentAccountId = bankAccount.Id,
                StartDate = new DateOnly(2026, 1, 1),
                Amount = 100m,
                Currency = "CHF",
                Frequency = ScheduleFrequency.Monthly,
                Interval = 1,
                DayOfMonth = 10,
                BusinessDayAdjustment = BusinessDayAdjustment.None,
                IsActive = true
            }
            ]
        };

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [bankAccount],
            transactions: [],
            pillar3aContracts: [pillar3a],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 3, 31));

        var engine = new SimulationEngine();

        var result = engine.Simulate(plan);

        var contributionEvents = result.Events
            .Where(x => x.Category == "Pillar 3a Contribution")
            .OrderBy(x => x.Date)
            .ToList();

        Assert.Equal(3, contributionEvents.Count);

        Assert.Equal(new DateOnly(2026, 1, 10), contributionEvents[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 10), contributionEvents[1].Date);
        Assert.Equal(new DateOnly(2026, 3, 10), contributionEvents[2].Date);

        Assert.Equal(9_700m, result.GetBalance(bankAccount.Id, new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void Simulate_Should_ApplyPlanBankOffDays_WhenScheduleUsesBusinessDayAdjustment()
    {
        // Arrange
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 1000m,
            openingDate: new DateOnly(2026, 8, 1));

        var invoice = TestPlanBuilder.ExternalExpense(
            fromAccountId: account.Id,
            amount: 100m,
            schedule: new Schedule
            {
                Frequency = ScheduleFrequency.Once,
                StartDate = new DateOnly(2026, 8, 1), // Saturday
                BusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay
            },
            name: "Invoice");

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            BaseCurrency = "CHF",
            Accounts = [account],
            Transactions = [invoice],
            TreatWeekendsAsBankOffDays = true,
            BankOffDays =
            [
                new BankOffDay
            {
                Date = new DateOnly(2026, 8, 3), // Monday
                Name = "Bank holiday"
            }
            ],
            SimulationSettings = new SimulationSettings
            {
                StartDate = new DateOnly(2026, 8, 1),
                EndDate = new DateOnly(2026, 8, 31)
            }
        };

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(plan);

        // Assert
        Assert.Single(result.Events);
        Assert.Equal(new DateOnly(2026, 8, 4), result.Events[0].Date);

        Assert.Equal(1000m, result.GetBalance(account.Id, new DateOnly(2026, 8, 3)));
        Assert.Equal(900m, result.GetBalance(account.Id, new DateOnly(2026, 8, 4)));
        Assert.Equal(900m, result.GetBalance(account.Id, new DateOnly(2026, 8, 31)));
    }
}