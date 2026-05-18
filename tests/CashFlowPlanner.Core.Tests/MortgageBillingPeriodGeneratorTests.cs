using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.Core.Tests;

public sealed class MortgageBillingPeriodGeneratorTests
{
    [Fact]
    public void GenerateBankQuarterPeriods_Should_GenerateQuarterPaymentDates()
    {
        var generator = new MortgageBillingPeriodGenerator();

        var periods = generator.GenerateBankQuarterPeriods(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Equal(4, periods.Count);

        Assert.Equal(new DateOnly(2026, 1, 1), periods[0].PeriodStart);
        Assert.Equal(new DateOnly(2026, 4, 1), periods[0].PeriodEndExclusive);
        Assert.Equal(new DateOnly(2026, 3, 31), periods[0].PaymentDate);

        Assert.Equal(new DateOnly(2026, 4, 1), periods[1].PeriodStart);
        Assert.Equal(new DateOnly(2026, 7, 1), periods[1].PeriodEndExclusive);
        Assert.Equal(new DateOnly(2026, 6, 30), periods[1].PaymentDate);

        Assert.Equal(new DateOnly(2026, 7, 1), periods[2].PeriodStart);
        Assert.Equal(new DateOnly(2026, 10, 1), periods[2].PeriodEndExclusive);
        Assert.Equal(new DateOnly(2026, 9, 30), periods[2].PaymentDate);

        Assert.Equal(new DateOnly(2026, 10, 1), periods[3].PeriodStart);
        Assert.Equal(new DateOnly(2027, 1, 1), periods[3].PeriodEndExclusive);
        Assert.Equal(new DateOnly(2026, 12, 31), periods[3].PaymentDate);
    }

    [Fact]
    public void PreviousBusinessDayStrict_Should_ReturnPreviousDay_WhenInputIsBusinessDay()
    {
        var date = MortgageBillingPeriodGenerator.PreviousBusinessDayStrict(
            new DateOnly(2026, 4, 1));

        Assert.Equal(new DateOnly(2026, 3, 31), date);
    }

    [Fact]
    public void PreviousBusinessDayStrict_Should_SkipWeekend()
    {
        // 2026-03-01 is Sunday.
        // Previous business day should be Friday 2026-02-27.
        var date = MortgageBillingPeriodGenerator.PreviousBusinessDayStrict(
            new DateOnly(2026, 3, 1));

        Assert.Equal(new DateOnly(2026, 2, 27), date);
    }

    [Fact]
    public void GenerateBankQuarterPeriods_Should_IncludeQuarter_WhenPaymentDateIsInsideRange()
    {
        var generator = new MortgageBillingPeriodGenerator();

        var periods = generator.GenerateBankQuarterPeriods(
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 3, 31));

        Assert.Single(periods);
        Assert.Equal(new DateOnly(2026, 1, 1), periods[0].PeriodStart);
        Assert.Equal(new DateOnly(2026, 4, 1), periods[0].PeriodEndExclusive);
        Assert.Equal(new DateOnly(2026, 3, 31), periods[0].PaymentDate);
    }
}