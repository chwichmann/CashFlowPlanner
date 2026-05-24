namespace CashFlowPlanner.Core;

public static class BankCalendarCalculator
{
    public static bool IsBankOffDay(DateOnly date, CashFlowPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.TreatWeekendsAsBankOffDays &&
            date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return true;
        }

        return plan.BankOffDays.Any(x => x.Date == date);
    }

    public static bool IsBankBusinessDay(DateOnly date, CashFlowPlan plan)
    {
        return !IsBankOffDay(date, plan);
    }

    public static DateOnly ApplyBusinessDayAdjustment(
        DateOnly date,
        CashFlowPlan plan,
        BusinessDayAdjustment adjustment)
    {
        return adjustment switch
        {
            BusinessDayAdjustment.None => date,
            BusinessDayAdjustment.NextBusinessDay => MoveToNextBankBusinessDay(date, plan),
            BusinessDayAdjustment.PreviousBusinessDay => MoveToPreviousBankBusinessDay(date, plan),
            _ => date
        };
    }

    public static DateOnly MoveToPreviousBankBusinessDay(DateOnly date, CashFlowPlan plan)
    {
        var result = date;

        while (!IsBankBusinessDay(result, plan))
        {
            result = result.AddDays(-1);
        }

        return result;
    }

    public static DateOnly MoveToNextBankBusinessDay(DateOnly date, CashFlowPlan plan)
    {
        var result = date;

        while (!IsBankBusinessDay(result, plan))
        {
            result = result.AddDays(1);
        }

        return result;
    }
}