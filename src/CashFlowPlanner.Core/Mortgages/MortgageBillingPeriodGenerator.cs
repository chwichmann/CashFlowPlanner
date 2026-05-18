namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgageBillingPeriodGenerator
{
    public IReadOnlyList<MortgageBillingPeriod> GenerateBankQuarterPeriods(
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        if (simulationEnd < simulationStart)
        {
            return [];
        }

        var periods = new List<MortgageBillingPeriod>();

        var startYear = simulationStart.Year - 1;
        var endYear = simulationEnd.Year + 1;

        for (var year = startYear; year <= endYear; year++)
        {
            AddQuarterPeriod(periods, year, 1, simulationStart, simulationEnd);
            AddQuarterPeriod(periods, year, 4, simulationStart, simulationEnd);
            AddQuarterPeriod(periods, year, 7, simulationStart, simulationEnd);
            AddQuarterPeriod(periods, year, 10, simulationStart, simulationEnd);
        }

        return periods
            .OrderBy(x => x.PaymentDate)
            .ToList();
    }

    public static DateOnly PreviousBusinessDayStrict(DateOnly date)
    {
        var current = date.AddDays(-1);

        while (IsWeekend(current))
        {
            current = current.AddDays(-1);
        }

        return current;
    }

    private static void AddQuarterPeriod(
        List<MortgageBillingPeriod> periods,
        int year,
        int quarterStartMonth,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        var periodStart = new DateOnly(year, quarterStartMonth, 1);
        var periodEndExclusive = periodStart.AddMonths(3);
        var paymentDate = PreviousBusinessDayStrict(periodEndExclusive);

        if (paymentDate < simulationStart || paymentDate > simulationEnd)
        {
            return;
        }

        periods.Add(new MortgageBillingPeriod(
            periodStart,
            periodEndExclusive,
            paymentDate));
    }

    private static bool IsWeekend(DateOnly date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }
}