namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgageBillingPeriodGenerator
{
    /// <summary>
    /// Billing periods of <paramref name="interval"/> length, anchored to the calendar year and
    /// paid on the last business day before the period ends.
    /// <para>
    /// Anchoring to January means the periods a Swiss bank actually bills: quarters are
    /// Jan-Mar, Apr-Jun, Jul-Sep, Oct-Dec regardless of when the mortgage was taken out, and
    /// half-years and years follow the same rule. Only the interval changes; the payment-date
    /// rule, the period arithmetic and the interest calculation are identical for all four.
    /// </para>
    /// </summary>
    public IReadOnlyList<MortgageBillingPeriod> GeneratePeriods(
        MortgagePaymentInterval interval,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        if (simulationEnd < simulationStart)
        {
            return [];
        }

        var intervalMonths = (int)interval;

        if (intervalMonths <= 0 || 12 % intervalMonths != 0)
        {
            throw new InvalidOperationException(
                $"Payment interval '{interval}' does not divide a calendar year evenly.");
        }

        var periods = new List<MortgageBillingPeriod>();

        var startYear = simulationStart.Year - 1;
        var endYear = simulationEnd.Year + 1;

        for (var year = startYear; year <= endYear; year++)
        {
            for (var month = 1; month <= 12; month += intervalMonths)
            {
                AddPeriod(periods, year, month, intervalMonths, simulationStart, simulationEnd);
            }
        }

        return periods
            .OrderBy(x => x.PaymentDate)
            .ToList();
    }

    /// <summary>Quarterly periods. Kept because most callers and tests mean exactly this.</summary>
    public IReadOnlyList<MortgageBillingPeriod> GenerateBankQuarterPeriods(
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        return GeneratePeriods(
            MortgagePaymentInterval.Quarterly,
            simulationStart,
            simulationEnd);
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

    private static void AddPeriod(
        List<MortgageBillingPeriod> periods,
        int year,
        int periodStartMonth,
        int intervalMonths,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        var periodStart = new DateOnly(year, periodStartMonth, 1);
        var periodEndExclusive = periodStart.AddMonths(intervalMonths);
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