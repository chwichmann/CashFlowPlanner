using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.Core.Tests;

/// <summary>
/// The mortgage editor has always offered monthly, quarterly, half-yearly and yearly payment
/// intervals in a plain dropdown, and the generator threw <c>NotSupportedException</c> on three
/// of the four. Picking one crashed the simulation, from a control that gave no hint it would.
/// <para>
/// Only the period length differed. Amortisation was already divided by <c>12 / interval</c>,
/// interest was already calculated from the period's own day count, and the payment-date rule -
/// the last business day before the period ends - is the same for all four. Only the period
/// generator was hardcoded to quarters.
/// </para>
/// </summary>
public sealed class MortgagePaymentIntervalTests
{
    private static (CashFlowPlan Plan, MortgageContract Mortgage) CreatePlan(
        MortgagePaymentInterval interval)
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
            AmortisationMode = AmortisationMode.Direct,
            AnnualAmortisationAmount = 12_000m,
            PaymentInterval = interval,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Interval plan",
            BaseCurrency = "CHF",
            Persons = [],
            Accounts = [savings],
            Transactions = [],
            Mortgages = [mortgage],
            CreditCards = [],
            Pillar3aContracts = [],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };

        return (plan, mortgage);
    }

    [Theory]
    [InlineData(MortgagePaymentInterval.Monthly, 12)]
    [InlineData(MortgagePaymentInterval.Quarterly, 4)]
    [InlineData(MortgagePaymentInterval.HalfYearly, 2)]
    [InlineData(MortgagePaymentInterval.Yearly, 1)]
    public void EveryInterval_Bills_ItsOwnNumberOfTimesPerYear(
        MortgagePaymentInterval interval,
        int expectedPayments)
    {
        var (plan, mortgage) = CreatePlan(interval);

        var result = new SimulationEngine().Simulate(plan);

        var amortisations = result.Events
            .Where(x => x.SourceTransactionId == mortgage.Id)
            .Where(x => x.Name.EndsWith("amortisation"))
            .ToList();

        var interest = result.Events
            .Where(x => x.SourceTransactionId == mortgage.Id)
            .Where(x => x.Name.EndsWith("interest"))
            .ToList();

        Assert.Equal(expectedPayments, amortisations.Count);
        Assert.Equal(expectedPayments, interest.Count);
    }

    [Theory]
    [InlineData(MortgagePaymentInterval.Monthly)]
    [InlineData(MortgagePaymentInterval.Quarterly)]
    [InlineData(MortgagePaymentInterval.HalfYearly)]
    [InlineData(MortgagePaymentInterval.Yearly)]
    public void EveryInterval_Amortises_TheSameAmountOverAYear(
        MortgagePaymentInterval interval)
    {
        // The instalment is the annual amount divided by the number of periods, so paying more
        // often must not pay down more debt. If it did, the interval would silently change the
        // plan's outcome rather than only its cash rhythm.
        var (plan, mortgage) = CreatePlan(interval);

        var result = new SimulationEngine().Simulate(plan);

        var amortised = result.Events
            .Where(x => x.SourceTransactionId == mortgage.Id)
            .Where(x => x.Name.EndsWith("amortisation"))
            .Sum(x => x.Amount);

        Assert.Equal(12_000m, amortised);

        Assert.Equal(
            708_000m,
            result.GetMortgagePrincipal(mortgage.Id, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void PayingMonthly_CostsLessInterest_ThanPayingYearly()
    {
        // Amortising earlier means interest accrues on a smaller principal for the rest of the
        // year. The difference is small, and it must exist: if the two agreed exactly, the
        // period boundaries would not be feeding into the interest calculation at all.
        var monthly = new SimulationEngine().Simulate(CreatePlan(MortgagePaymentInterval.Monthly).Plan);
        var yearly = new SimulationEngine().Simulate(CreatePlan(MortgagePaymentInterval.Yearly).Plan);

        var monthlyInterest = monthly.Events.Where(x => x.Name.EndsWith("interest")).Sum(x => x.Amount);
        var yearlyInterest = yearly.Events.Where(x => x.Name.EndsWith("interest")).Sum(x => x.Amount);

        Assert.True(
            monthlyInterest < yearlyInterest,
            $"monthly cost {monthlyInterest:N2} and yearly cost {yearlyInterest:N2} - "
            + "amortisation is not reducing the base the next period charges on");
    }

    [Fact]
    public void MonthlyPayments_FallOn_TheLastBusinessDayOfEachMonth()
    {
        var (plan, mortgage) = CreatePlan(MortgagePaymentInterval.Monthly);

        var result = new SimulationEngine().Simulate(plan);

        var dates = result.Events
            .Where(x => x.SourceTransactionId == mortgage.Id)
            .Where(x => x.Name.EndsWith("interest"))
            .Select(x => x.Date)
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(12, dates.Count);

        foreach (var date in dates)
        {
            Assert.NotEqual(DayOfWeek.Saturday, date.DayOfWeek);
            Assert.NotEqual(DayOfWeek.Sunday, date.DayOfWeek);

            // The last business day of the month: the next business day is in the next month.
            var next = date.AddDays(1);

            while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                next = next.AddDays(1);
            }

            Assert.NotEqual(date.Month, next.Month);
        }
    }
}
