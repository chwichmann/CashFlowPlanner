namespace CashFlowPlanner.Core;

public sealed class ScheduleOccurrenceGenerator
{
    public IReadOnlyList<DateOnly> GenerateOccurrences(
        Schedule schedule,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CashFlowPlan? plan = null)
    {
        schedule.Validate();

        if (rangeEnd < rangeStart)
        {
            return [];
        }

        if (schedule.EndDate is not null && schedule.EndDate < schedule.StartDate)
        {
            return [];
        }

        return schedule.Frequency switch
        {
            ScheduleFrequency.Once => GenerateOnce(schedule, rangeStart, rangeEnd, plan),
            ScheduleFrequency.Daily => GenerateDaily(schedule, rangeStart, rangeEnd, plan),
            ScheduleFrequency.Weekly => GenerateWeekly(schedule, rangeStart, rangeEnd, plan),
            ScheduleFrequency.Monthly => GenerateMonthly(schedule, rangeStart, rangeEnd, plan),
            ScheduleFrequency.Quarterly => GenerateMonthlyLike(schedule, rangeStart, rangeEnd, 3, plan),
            ScheduleFrequency.SemiYearly => GenerateMonthlyLike(schedule, rangeStart, rangeEnd, 6, plan),
            ScheduleFrequency.Yearly => GenerateYearly(schedule, rangeStart, rangeEnd, plan),
            _ => throw new InvalidOperationException($"Unsupported schedule frequency '{schedule.Frequency}'.")
        };
    }

    private static IReadOnlyList<DateOnly> GenerateOnce(
        Schedule schedule,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CashFlowPlan? plan)
    {
        if (!IsNominalDateInsideSchedule(schedule, schedule.StartDate))
        {
            return [];
        }

        var adjusted = ApplyBusinessDayAdjustment(
            schedule.StartDate,
            schedule.BusinessDayAdjustment,
            plan);

        return adjusted >= rangeStart && adjusted <= rangeEnd
            ? [adjusted]
            : [];
    }

    private static IReadOnlyList<DateOnly> GenerateDaily(
        Schedule schedule,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CashFlowPlan? plan)
    {
        var result = new List<DateOnly>();

        var nominalEnd = schedule.EndDate is null
            ? rangeEnd
            : Min(schedule.EndDate.Value, rangeEnd.AddDays(7));

        for (var nominalDate = schedule.StartDate;
             nominalDate <= nominalEnd;
             nominalDate = nominalDate.AddDays(schedule.Interval))
        {
            if (!IsNominalDateInsideSchedule(schedule, nominalDate))
            {
                continue;
            }

            var adjusted = ApplyBusinessDayAdjustment(
                nominalDate,
                schedule.BusinessDayAdjustment,
                plan);

            if (adjusted >= rangeStart && adjusted <= rangeEnd)
            {
                result.Add(adjusted);
            }
        }

        // No Distinct(): when the business-day adjustment maps several nominal
        // dates onto the same business day those are collisions, not duplicates.
        // A daily expense that falls on Sat/Sun/Mon is still three payments.
        return result.Order().ToList();
    }

    private static IReadOnlyList<DateOnly> GenerateWeekly(
        Schedule schedule,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CashFlowPlan? plan)
    {
        var result = new List<DateOnly>();

        var wantedDay = schedule.DayOfWeek ?? schedule.StartDate.DayOfWeek;
        var first = schedule.StartDate;

        while (first.DayOfWeek != wantedDay)
        {
            first = first.AddDays(1);
        }

        var nominalEnd = schedule.EndDate is null
            ? rangeEnd
            : Min(schedule.EndDate.Value, rangeEnd.AddDays(7));

        for (var nominalDate = first;
             nominalDate <= nominalEnd;
             nominalDate = nominalDate.AddDays(7 * schedule.Interval))
        {
            if (!IsNominalDateInsideSchedule(schedule, nominalDate))
            {
                continue;
            }

            var adjusted = ApplyBusinessDayAdjustment(
                nominalDate,
                schedule.BusinessDayAdjustment,
                plan);

            if (adjusted >= rangeStart && adjusted <= rangeEnd)
            {
                result.Add(adjusted);
            }
        }

        // No Distinct(): when the business-day adjustment maps several nominal
        // dates onto the same business day those are collisions, not duplicates.
        // A daily expense that falls on Sat/Sun/Mon is still three payments.
        return result.Order().ToList();
    }

    private static IReadOnlyList<DateOnly> GenerateMonthly(
        Schedule schedule,
        DateOnly effectiveStart,
        DateOnly effectiveEnd,
        CashFlowPlan? plan)
    {
        return GenerateMonthlyLike(schedule, effectiveStart, effectiveEnd, 1, plan);
    }

    private static IReadOnlyList<DateOnly> GenerateMonthlyLike(
        Schedule schedule,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        int monthStep,
        CashFlowPlan? plan)
    {
        var result = new List<DateOnly>();

        var current = new DateOnly(schedule.StartDate.Year, schedule.StartDate.Month, 1);
        var day = schedule.DayOfMonth ?? schedule.StartDate.Day;
        var effectiveMonthStep = monthStep * schedule.Interval;

        var nominalEnd = schedule.EndDate is null
            ? rangeEnd.AddDays(7)
            : Min(schedule.EndDate.Value, rangeEnd.AddDays(7));

        while (current <= nominalEnd)
        {
            var nominalDate = CreateDateClampedToMonth(
                current.Year,
                current.Month,
                day);

            if (IsNominalDateInsideSchedule(schedule, nominalDate))
            {
                var adjusted = ApplyBusinessDayAdjustment(
                    nominalDate,
                    schedule.BusinessDayAdjustment,
                    plan);

                if (adjusted >= rangeStart && adjusted <= rangeEnd)
                {
                    result.Add(adjusted);
                }
            }

            current = current.AddMonths(effectiveMonthStep);
        }

        // No Distinct(): when the business-day adjustment maps several nominal
        // dates onto the same business day those are collisions, not duplicates.
        // A daily expense that falls on Sat/Sun/Mon is still three payments.
        return result.Order().ToList();
    }

    private static IReadOnlyList<DateOnly> GenerateYearly(
        Schedule schedule,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CashFlowPlan? plan)
    {
        var result = new List<DateOnly>();

        var month = schedule.Month ?? schedule.StartDate.Month;
        var day = schedule.DayOfMonth ?? schedule.StartDate.Day;

        var nominalEnd = schedule.EndDate is null
            ? rangeEnd.AddDays(7)
            : Min(schedule.EndDate.Value, rangeEnd.AddDays(7));

        for (var year = schedule.StartDate.Year; year <= nominalEnd.Year; year += schedule.Interval)
        {
            var nominalDate = CreateDateClampedToMonth(year, month, day);

            if (!IsNominalDateInsideSchedule(schedule, nominalDate))
            {
                continue;
            }

            var adjusted = ApplyBusinessDayAdjustment(
                nominalDate,
                schedule.BusinessDayAdjustment,
                plan);

            if (adjusted >= rangeStart && adjusted <= rangeEnd)
            {
                result.Add(adjusted);
            }
        }

        // No Distinct(): when the business-day adjustment maps several nominal
        // dates onto the same business day those are collisions, not duplicates.
        // A daily expense that falls on Sat/Sun/Mon is still three payments.
        return result.Order().ToList();
    }

    private static DateOnly ApplyBusinessDayAdjustment(
        DateOnly date,
        BusinessDayAdjustment adjustment,
        CashFlowPlan? plan)
    {
        if (plan is not null)
        {
            return BankCalendarCalculator.ApplyBusinessDayAdjustment(
                date,
                plan,
                adjustment);
        }

        return adjustment switch
        {
            BusinessDayAdjustment.None => date,
            BusinessDayAdjustment.NextBusinessDay => MoveToNextBusinessDay(date),
            BusinessDayAdjustment.PreviousBusinessDay => MoveToPreviousBusinessDay(date),
            _ => date
        };
    }

    private static DateOnly CreateDateClampedToMonth(int year, int month, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(day, daysInMonth));
    }

    private static DateOnly MoveToNextBusinessDay(DateOnly date)
    {
        while (IsWeekend(date))
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static DateOnly MoveToPreviousBusinessDay(DateOnly date)
    {
        while (IsWeekend(date))
        {
            date = date.AddDays(-1);
        }

        return date;
    }

    private static bool IsWeekend(DateOnly date)
        => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static DateOnly Min(DateOnly left, DateOnly right)
        => left <= right ? left : right;

    private static bool IsNominalDateInsideSchedule(
        Schedule schedule,
        DateOnly nominalDate)
    {
        if (nominalDate < schedule.StartDate)
        {
            return false;
        }

        if (schedule.EndDate is not null && nominalDate > schedule.EndDate.Value)
        {
            return false;
        }

        return true;
    }
}