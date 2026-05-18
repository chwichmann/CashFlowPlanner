using CashFlowPlanner.Core;

namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aContributionSchedule
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid PaymentAccountId { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "CHF";

    public ScheduleFrequency Frequency { get; init; } = ScheduleFrequency.Monthly;

    public int Interval { get; init; } = 1;

    public int? DayOfMonth { get; init; }

    public DayOfWeek? DayOfWeek { get; init; }

    public int? Month { get; init; }

    public BusinessDayAdjustment BusinessDayAdjustment { get; init; } = BusinessDayAdjustment.None;

    public bool IsActive { get; init; } = true;

    public string? Notes { get; init; }

    public Schedule ToSchedule()
    {
        return new Schedule
        {
            Frequency = Frequency,
            StartDate = StartDate,
            EndDate = EndDate,
            Interval = Interval,
            DayOfMonth = DayOfMonth,
            DayOfWeek = DayOfWeek,
            Month = Month,
            BusinessDayAdjustment = BusinessDayAdjustment
        };
    }

    public void Validate(string contractName)
    {
        if (PaymentAccountId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contribution schedule for contract '{contractName}' requires a payment account.");
        }

        if (Amount <= 0m)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contribution schedule for contract '{contractName}' requires a positive amount.");
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            throw new InvalidOperationException(
                $"Pillar 3a contribution schedule for contract '{contractName}' requires a currency.");
        }

        if (Interval < 1)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contribution schedule for contract '{contractName}' requires an interval greater than or equal to 1.");
        }

        if (EndDate is not null && EndDate.Value < StartDate)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contribution schedule for contract '{contractName}' has an end date before the start date.");
        }

        ToSchedule().Validate();
    }
}