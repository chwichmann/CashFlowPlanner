using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Tests.Accounts;

public sealed class InterestDayCountCalculatorTests
{
    [Fact]
    public void GetYearFraction_Actual360_UsesActualDaysOver360()
    {
        var result = InterestDayCountCalculator.GetYearFraction(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            InterestDayCountConvention.Actual360);

        Assert.Equal(31m / 360m, result);
    }

    [Fact]
    public void GetYearFraction_Actual365_UsesActualDaysOver365()
    {
        var result = InterestDayCountCalculator.GetYearFraction(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            InterestDayCountConvention.Actual365);

        Assert.Equal(31m / 365m, result);
    }

    [Fact]
    public void GetYearFraction_ActualActual_LeapYear_Uses366()
    {
        var result = InterestDayCountCalculator.GetYearFraction(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 2, 1),
            InterestDayCountConvention.ActualActual);

        Assert.Equal(31m / 366m, result);
    }

    [Fact]
    public void GetYearFraction_ActualActual_CrossYear_SplitsByYear()
    {
        var result = InterestDayCountCalculator.GetYearFraction(
            new DateOnly(2023, 12, 31),
            new DateOnly(2024, 1, 2),
            InterestDayCountConvention.ActualActual);

        var expected = (1m / 365m) + (1m / 366m);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetYearFraction_Thirty360_UsesThirtyDayMonth()
    {
        var result = InterestDayCountCalculator.GetYearFraction(
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 2, 28),
            InterestDayCountConvention.Thirty360);

        Assert.Equal(28m / 360m, result);
    }

    [Fact]
    public void GetYearFraction_EndBeforeStart_ReturnsZero()
    {
        var result = InterestDayCountCalculator.GetYearFraction(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 1, 1),
            InterestDayCountConvention.Actual360);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void GetYearFraction_SameDate_ReturnsZero()
    {
        var result = InterestDayCountCalculator.GetYearFraction(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 1),
            InterestDayCountConvention.Actual360);

        Assert.Equal(0m, result);
    }
}