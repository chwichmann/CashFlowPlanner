namespace CashFlowPlanner.Core.Analysis;

public static class ScheduledTransactionYearCalculator
{
    public static int CountOccurrencesInYear(Schedule schedule, int year)
    {
        if (schedule.Interval < 1)
        {
            throw new InvalidOperationException("Schedule interval must be greater than or equal to 1.");
        }

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var effectiveStart = schedule.StartDate > yearStart
            ? schedule.StartDate
            : yearStart;

        var effectiveEnd = schedule.EndDate is not null && schedule.EndDate.Value < yearEnd
            ? schedule.EndDate.Value
            : yearEnd;

        if (effectiveEnd < effectiveStart)
        {
            return 0;
        }

        return schedule.Frequency switch
        {
            ScheduleFrequency.Once =>
                CountOnce(schedule, year, effectiveStart, effectiveEnd),

            ScheduleFrequency.Daily =>
                CountByStep(
                    schedule.StartDate,
                    effectiveStart,
                    effectiveEnd,
                    current => current.AddDays(schedule.Interval)),

            ScheduleFrequency.Weekly =>
                CountByStep(
                    schedule.StartDate,
                    effectiveStart,
                    effectiveEnd,
                    current => current.AddDays(7 * schedule.Interval)),

            ScheduleFrequency.Monthly =>
                CountByStep(
                    schedule.StartDate,
                    effectiveStart,
                    effectiveEnd,
                    current => current.AddMonths(schedule.Interval)),

            ScheduleFrequency.Quarterly =>
                CountByStep(
                    schedule.StartDate,
                    effectiveStart,
                    effectiveEnd,
                    current => current.AddMonths(3 * schedule.Interval)),

            ScheduleFrequency.SemiYearly =>
                CountByStep(
                    schedule.StartDate,
                    effectiveStart,
                    effectiveEnd,
                    current => current.AddMonths(6 * schedule.Interval)),

            ScheduleFrequency.Yearly =>
                CountByStep(
                    schedule.StartDate,
                    effectiveStart,
                    effectiveEnd,
                    current => current.AddYears(schedule.Interval)),

            _ => 0
        };
    }

    private static int CountOnce(
        Schedule schedule,
        int year,
        DateOnly effectiveStart,
        DateOnly effectiveEnd)
    {
        if (schedule.StartDate.Year != year)
        {
            return 0;
        }

        return schedule.StartDate >= effectiveStart &&
               schedule.StartDate <= effectiveEnd
            ? 1
            : 0;
    }

    private static int CountByStep(
        DateOnly firstDate,
        DateOnly effectiveStart,
        DateOnly effectiveEnd,
        Func<DateOnly, DateOnly> getNextDate)
    {
        var count = 0;
        var current = firstDate;

        while (current <= effectiveEnd)
        {
            if (current >= effectiveStart)
            {
                count++;
            }

            var next = getNextDate(current);

            if (next <= current)
            {
                throw new InvalidOperationException("Schedule calculation did not progress.");
            }

            current = next;
        }

        return count;
    }
}