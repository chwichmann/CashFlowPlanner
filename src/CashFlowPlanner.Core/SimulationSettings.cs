namespace CashFlowPlanner.Core;

public sealed class SimulationSettings
{
    public DateOnly StartDate { get; init; } = GetDefaultStartDate();

    public DateOnly EndDate { get; init; } = GetDefaultEndDate();

    public SimulationDateMode DateMode { get; init; } = SimulationDateMode.RollingHorizon;

    public SimulationStartAnchor StartAnchor { get; init; } = SimulationStartAnchor.FirstDayOfCurrentMonth;

    public int HorizonMonths { get; init; } = 12;

    public SimulationGranularity Granularity { get; init; } = SimulationGranularity.Daily;

    public bool IncludeInactiveAccounts { get; init; } = false;

    public bool WarnOnNegativeBankBalance { get; init; } = true;

    public void Validate()
    {
        if (DateMode == SimulationDateMode.ExplicitDateRange &&
            EndDate < StartDate)
        {
            throw new InvalidOperationException("Simulation EndDate must be greater than or equal to StartDate.");
        }

        if (DateMode == SimulationDateMode.RollingHorizon &&
            HorizonMonths < 1)
        {
            throw new InvalidOperationException("Simulation horizon must be at least one month.");
        }
    }

    private static DateOnly GetDefaultStartDate()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return new DateOnly(today.Year, today.Month, 1);
    }

    private static DateOnly GetDefaultEndDate()
    {
        return GetDefaultStartDate()
            .AddYears(1)
            .AddDays(-1);
    }

    public SimulationDateRange GetEffectiveDateRange(DateOnly today)
    {
        if (DateMode == SimulationDateMode.ExplicitDateRange)
        {
            return new SimulationDateRange(StartDate, EndDate);
        }

        var startDate = StartAnchor switch
        {
            SimulationStartAnchor.Today =>
                today,

            SimulationStartAnchor.FirstDayOfCurrentMonth =>
                new DateOnly(today.Year, today.Month, 1),

            SimulationStartAnchor.FirstDayOfCurrentYear =>
                new DateOnly(today.Year, 1, 1),

            _ =>
                new DateOnly(today.Year, today.Month, 1)
        };

        var months = HorizonMonths < 1
            ? 1
            : HorizonMonths;

        var endDate = startDate
            .AddMonths(months)
            .AddDays(-1);

        return new SimulationDateRange(startDate, endDate);
    }
}