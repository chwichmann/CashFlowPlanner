using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.Core.Tests;

/// <summary>
/// Mortgage payment dates stepped back off weekends and nothing else, while every transaction
/// schedule in the same plan honoured <see cref="CashFlowPlan.BankOffDays"/>. So a quarterly
/// instalment could be dated 31 December or 1 August - days the plan itself says the bank is
/// shut - and the daily liquidity curve showed money leaving on a day it could not.
/// </summary>
public sealed class MortgageBankHolidayTests
{
    private static CashFlowPlan CreatePlan(params BankOffDay[] offDays)
    {
        var savings = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Savings",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = 200_000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var mortgage = new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "House Mortgage",
            Type = MortgageType.Fixed,
            PaymentAccountId = savings.Id,
            InitialPrincipal = 720_000m,
            InitialDate = new DateOnly(2026, 1, 1),
            CalculationPrincipal = 720_000m,
            CalculationPrincipalDate = new DateOnly(2026, 1, 1),
            FixedInterestPercent = 1.5m,
            AmortisationMode = AmortisationMode.None,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };

        return new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Holiday plan",
            BaseCurrency = "CHF",
            Persons = [],
            Accounts = [savings],
            Transactions = [],
            Mortgages = [mortgage],
            CreditCards = [],
            Pillar3aContracts = [],
            TreatWeekendsAsBankOffDays = true,
            BankOffDays = [.. offDays],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };
    }

    private static List<DateOnly> PaymentDates(CashFlowPlan plan)
    {
        var mortgageId = plan.Mortgages.Single().Id;

        return new SimulationEngine()
            .Simulate(plan)
            .Events
            .Where(x => x.SourceTransactionId == mortgageId)
            .Select(x => x.Date)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    [Fact]
    public void WithNoBankHolidays_PaymentsFall_OnTheLastWeekdayOfEachQuarter()
    {
        var dates = PaymentDates(CreatePlan());

        Assert.Equal(
            [
                new DateOnly(2026, 3, 31),
                new DateOnly(2026, 6, 30),
                new DateOnly(2026, 9, 30),
                new DateOnly(2026, 12, 31)
            ],
            dates);
    }

    [Fact]
    public void ABankHolidayOnTheDueDate_MovesThePayment_Earlier()
    {
        // 31 December 2026 is a Thursday - a business day by the weekend rule alone, and shut
        // in the plan the user actually configured.
        var plan = CreatePlan(
            new BankOffDay { Date = new DateOnly(2026, 12, 31), Name = "Silvester" });

        var dates = PaymentDates(plan);

        Assert.Equal(new DateOnly(2026, 12, 30), dates[^1]);
    }

    [Fact]
    public void ARunOfBankHolidays_MovesThePayment_PastAllOfThem()
    {
        var plan = CreatePlan(
            new BankOffDay { Date = new DateOnly(2026, 12, 31), Name = "Silvester" },
            new BankOffDay { Date = new DateOnly(2026, 12, 30), Name = "Betriebsferien" },
            new BankOffDay { Date = new DateOnly(2026, 12, 29), Name = "Betriebsferien" });

        var dates = PaymentDates(plan);

        Assert.Equal(new DateOnly(2026, 12, 28), dates[^1]);
    }

    [Fact]
    public void ABankHolidayNotOnADueDate_ChangesNothing()
    {
        // 1 August 2026 is a Saturday, so the Q3 payment was never near it. A plan-level
        // holiday must not move a payment that was not on it.
        var plan = CreatePlan(
            new BankOffDay { Date = new DateOnly(2026, 8, 1), Name = "Bundesfeier" });

        Assert.Equal(PaymentDates(CreatePlan()), PaymentDates(plan));
    }
}
