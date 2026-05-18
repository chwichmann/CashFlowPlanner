using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Analysis;

namespace CashFlowPlanner.Core.Tests.Analysis;

public sealed class ScheduledTransactionYearCalculatorTests
{
    [Fact]
    public void CountOccurrencesInYear_MonthlyFullYear_ReturnsTwelve()
    {
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            Interval = 1
        };

        var result = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
            schedule,
            2026);

        Assert.Equal(12, result);
    }

    [Fact]
    public void CountOccurrencesInYear_MonthlyStartingInMarch_ReturnsTen()
    {
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 3, 1),
            Interval = 1
        };

        var result = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
            schedule,
            2026);

        Assert.Equal(10, result);
    }

    [Fact]
    public void CountOccurrencesInYear_MonthlyEndingInJune_ReturnsSix()
    {
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 6, 30),
            Interval = 1
        };

        var result = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
            schedule,
            2026);

        Assert.Equal(6, result);
    }

    [Fact]
    public void CountOccurrencesInYear_OnceInsideYear_ReturnsOne()
    {
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Once,
            StartDate = new DateOnly(2026, 7, 15),
            Interval = 1
        };

        var result = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
            schedule,
            2026);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CountOccurrencesInYear_OnceOutsideYear_ReturnsZero()
    {
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Once,
            StartDate = new DateOnly(2025, 7, 15),
            Interval = 1
        };

        var result = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
            schedule,
            2026);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountOccurrencesInYear_QuarterlyFullYear_ReturnsFour()
    {
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Quarterly,
            StartDate = new DateOnly(2026, 1, 1),
            Interval = 1
        };

        var result = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
            schedule,
            2026);

        Assert.Equal(4, result);
    }

    [Fact]
    public void CountOccurrencesInYear_YearlyFullYear_ReturnsOne()
    {
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Yearly,
            StartDate = new DateOnly(2026, 1, 1),
            Interval = 1
        };

        var result = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
            schedule,
            2026);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CountOccurrencesInYear_InvalidInterval_Throws()
    {
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            Interval = 0
        };

        Assert.Throws<InvalidOperationException>(() =>
            ScheduledTransactionYearCalculator.CountOccurrencesInYear(
                schedule,
                2026));
    }
}