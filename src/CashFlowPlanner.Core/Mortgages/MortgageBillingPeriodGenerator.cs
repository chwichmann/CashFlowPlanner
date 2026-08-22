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
        DateOnly simulationEnd,
        CashFlowPlan? bankCalendar = null)
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
                AddPeriod(
                    periods,
                    year,
                    month,
                    intervalMonths,
                    simulationStart,
                    simulationEnd,
                    bankCalendar);
            }
        }

        return periods
            .OrderBy(x => x.PaymentDate)
            .ToList();
    }

    /// <summary>Quarterly periods. Kept because most callers and tests mean exactly this.</summary>
    public IReadOnlyList<MortgageBillingPeriod> GenerateBankQuarterPeriods(
        DateOnly simulationStart,
        DateOnly simulationEnd,
        CashFlowPlan? bankCalendar = null)
    {
        return GeneratePeriods(
            MortgagePaymentInterval.Quarterly,
            simulationStart,
            simulationEnd,
            bankCalendar);
    }

    /// <summary>
    /// The last business day strictly before <paramref name="date"/>.
    ///
    /// With a <paramref name="bankCalendar"/> this honours the plan's own bank-off days and
    /// its weekend setting, the same calendar every transaction schedule already uses. Without
    /// one it skips weekends only, which is what this did before the plan was available here -
    /// and it is why a quarterly mortgage payment could land on 1 August or 25 December while
    /// every ordinary standing order beside it correctly stepped back.
    /// </summary>
    public static DateOnly PreviousBusinessDayStrict(
        DateOnly date,
        CashFlowPlan? bankCalendar = null)
    {
        var current = date.AddDays(-1);

        if (bankCalendar is null)
        {
            while (IsWeekend(current))
            {
                current = current.AddDays(-1);
            }

            return current;
        }

        return BankCalendarCalculator.MoveToPreviousBankBusinessDay(current, bankCalendar);
    }

    private static void AddPeriod(
        List<MortgageBillingPeriod> periods,
        int year,
        int periodStartMonth,
        int intervalMonths,
        DateOnly simulationStart,
        DateOnly simulationEnd,
        CashFlowPlan? bankCalendar)
    {
        var periodStart = new DateOnly(year, periodStartMonth, 1);
        var periodEndExclusive = periodStart.AddMonths(intervalMonths);
        var paymentDate = PreviousBusinessDayStrict(periodEndExclusive, bankCalendar);

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