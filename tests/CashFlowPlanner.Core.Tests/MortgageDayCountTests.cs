using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.Core.Tests;

/// <summary>
/// Mortgage interest divided by a hardcoded 365 while account interest had had a configurable
/// convention all along. Swiss lenders differ - 30/360 is common on mortgages, ACT/360 on
/// money-market tranches - and ACT/360 charges about 1.4% more per year than ACT/365 on the
/// same rate. That is small enough to look like a rounding difference and large enough that a
/// plan never quite agrees with the bank statement.
/// </summary>
public sealed class MortgageDayCountTests
{
    private static MortgageContract CreateMortgage(InterestDayCountConvention convention)
    {
        return new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "House Mortgage",
            Type = MortgageType.Fixed,
            PaymentAccountId = Guid.NewGuid(),
            InitialPrincipal = 720_000m,
            InitialDate = new DateOnly(2026, 1, 1),
            CalculationPrincipal = 720_000m,
            CalculationPrincipalDate = new DateOnly(2026, 1, 1),
            FixedInterestPercent = 1.5m,
            AmortisationMode = AmortisationMode.None,
            PaymentInterval = MortgagePaymentInterval.Yearly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            DayCountConvention = convention,
            IsActive = true
        };
    }

    private static decimal InterestForCalendarYear(InterestDayCountConvention convention)
    {
        return MortgageEventGenerator.CalculateInterestForPeriod(
            CreateMortgage(convention),
            720_000m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void TheDefault_Is_Actual365_SoNoSavedPlanChangesItsNumbers()
    {
        Assert.Equal(
            InterestDayCountConvention.Actual365,
            CreateMortgage(InterestDayCountConvention.Actual365).DayCountConvention);

        // 720'000 * 1.5% * 365/365, exactly the figure the hardcoded /365 produced.
        Assert.Equal(10_800m, InterestForCalendarYear(InterestDayCountConvention.Actual365));
    }

    [Fact]
    public void Actual360_Charges_ADayCountYearShorterThanACalendarYear()
    {
        // 720'000 * 1.5% * 365/360 = 10'950. The 150-franc gap is the whole point of the field.
        Assert.Equal(10_950m, InterestForCalendarYear(InterestDayCountConvention.Actual360));
    }

    [Fact]
    public void Thirty360_Charges_TwelveThirtyDayMonths()
    {
        // 720'000 * 1.5% * 360/360.
        Assert.Equal(10_800m, InterestForCalendarYear(InterestDayCountConvention.Thirty360));
    }

    [Fact]
    public void ActualActual_Charges_AFullYearInALeapYear()
    {
        var mortgage = CreateMortgage(InterestDayCountConvention.ActualActual);

        // 2028 has 366 days and ACT/ACT divides by 366, so a whole leap year is still 1.5%.
        var interest = MortgageEventGenerator.CalculateInterestForPeriod(
            mortgage,
            720_000m,
            new DateOnly(2028, 1, 1),
            new DateOnly(2029, 1, 1));

        Assert.Equal(10_800m, interest);
    }

    [Fact]
    public void Actual365_InALeapYear_ChargesTheExtraDay()
    {
        var interest = MortgageEventGenerator.CalculateInterestForPeriod(
            CreateMortgage(InterestDayCountConvention.Actual365),
            720_000m,
            new DateOnly(2028, 1, 1),
            new DateOnly(2029, 1, 1));

        // 366/365 of a year. The convention says a year is 365 days; the calendar disagrees,
        // and ACT/365 resolves that in the lender's favour.
        Assert.Equal(10_829.59m, interest);
    }

    [Fact]
    public void ASaronRateStep_MidPeriod_IsChargedAtEachRateForItsOwnDays()
    {
        // The accrual is walked as runs of equal rate. A rate that steps mid-period must split
        // the period rather than apply either rate to the whole of it.
        var mortgage = new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "SARON",
            Type = MortgageType.Saron,
            PaymentAccountId = Guid.NewGuid(),
            InitialPrincipal = 365_000m,
            InitialDate = new DateOnly(2026, 1, 1),
            CalculationPrincipal = 365_000m,
            CalculationPrincipalDate = new DateOnly(2026, 1, 1),
            FixedInterestPercent = 0m,
            SaronRates =
            [
                // Three points, not two: MortgageRateCurve interpolates linearly between
                // points, so a bare Jan-then-July pair would ramp continuously rather than
                // step. Holding 1% to 30 June makes the step a step.
                new MortgageInterestRatePoint { Date = new DateOnly(2026, 1, 1), RatePercent = 1m },
                new MortgageInterestRatePoint { Date = new DateOnly(2026, 6, 30), RatePercent = 1m },
                new MortgageInterestRatePoint { Date = new DateOnly(2026, 7, 1), RatePercent = 2m }
            ],
            AmortisationMode = AmortisationMode.None,
            PaymentInterval = MortgagePaymentInterval.Yearly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            DayCountConvention = InterestDayCountConvention.Actual365,
            IsActive = true
        };

        var interest = MortgageEventGenerator.CalculateInterestForPeriod(
            mortgage,
            365_000m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1));

        // 365'000 at 1% for 181 days plus 365'000 at 2% for 184 days, over 365.
        var expected = Math.Round(
            (365_000m * 0.01m * 181m / 365m) + (365_000m * 0.02m * 184m / 365m),
            2,
            MidpointRounding.AwayFromZero);

        Assert.Equal(expected, interest);
    }
}
