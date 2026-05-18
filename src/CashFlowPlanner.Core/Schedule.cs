namespace CashFlowPlanner.Core;

public sealed class Schedule
{
    public ScheduleFrequency Frequency { get; init; } = ScheduleFrequency.Once;

    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    /// <summary>
    /// Every n units.
    /// Example:
    /// Monthly + Interval 1 = every month.
    /// Weekly + Interval 2 = every second week.
    /// </summary>
    public int Interval { get; init; } = 1;

    /// <summary>
    /// Used for monthly, quarterly, semi-yearly and yearly schedules.
    /// If null, the day from StartDate is used.
    /// </summary>
    public int? DayOfMonth { get; init; }

    /// <summary>
    /// Used for weekly schedules.
    /// If null, the day from StartDate is used.
    /// </summary>
    public DayOfWeek? DayOfWeek { get; init; }

    /// <summary>
    /// Used for yearly schedules.
    /// If null, the month from StartDate is used.
    /// </summary>
    public int? Month { get; init; }

    public BusinessDayAdjustment BusinessDayAdjustment { get; init; } = BusinessDayAdjustment.None;

    public void Validate()
    {
        if (Interval < 1)
        {
            throw new InvalidOperationException("Schedule interval must be >= 1.");
        }

        if (DayOfMonth is < 1 or > 31)
        {
            throw new InvalidOperationException("DayOfMonth must be between 1 and 31.");
        }

        if (Month is < 1 or > 12)
        {
            throw new InvalidOperationException("Month must be between 1 and 12.");
        }

        if (EndDate is not null && EndDate < StartDate)
        {
            throw new InvalidOperationException("Schedule EndDate must be greater than or equal to StartDate.");
        }
    }
}