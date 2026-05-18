namespace CashFlowPlanner.Core.Accounts;

public static class InterestDayCountCalculator
{
    public static decimal GetYearFraction(
        DateOnly startDateInclusive,
        DateOnly endDateExclusive,
        InterestDayCountConvention convention)
    {
        if (endDateExclusive <= startDateInclusive)
        {
            return 0m;
        }

        var actualDays = endDateExclusive.DayNumber - startDateInclusive.DayNumber;

        return convention switch
        {
            InterestDayCountConvention.Actual360 =>
                actualDays / 360m,

            InterestDayCountConvention.Actual365 =>
                actualDays / 365m,

            InterestDayCountConvention.ActualActual =>
                CalculateActualActual(startDateInclusive, endDateExclusive),

            InterestDayCountConvention.Thirty360 =>
                CalculateThirty360(startDateInclusive, endDateExclusive) / 360m,

            _ =>
                actualDays / 360m
        };
    }

    private static decimal CalculateActualActual(
        DateOnly startDateInclusive,
        DateOnly endDateExclusive)
    {
        var total = 0m;
        var current = startDateInclusive;

        while (current < endDateExclusive)
        {
            var nextYearStart = new DateOnly(current.Year + 1, 1, 1);

            var segmentEnd = nextYearStart < endDateExclusive
                ? nextYearStart
                : endDateExclusive;

            var days = segmentEnd.DayNumber - current.DayNumber;
            var daysInYear = DateTime.IsLeapYear(current.Year) ? 366m : 365m;

            total += days / daysInYear;

            current = segmentEnd;
        }

        return total;
    }

    private static int CalculateThirty360(
        DateOnly startDateInclusive,
        DateOnly endDateExclusive)
    {
        var d1 = Math.Min(startDateInclusive.Day, 30);
        var d2 = Math.Min(endDateExclusive.Day, 30);

        return
            (endDateExclusive.Year - startDateInclusive.Year) * 360 +
            (endDateExclusive.Month - startDateInclusive.Month) * 30 +
            (d2 - d1);
    }
}