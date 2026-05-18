using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class Pillar3aContributionScheduleEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? PaymentAccountId { get; set; }

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? EndDate { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CHF";

    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Monthly;

    public int Interval { get; set; } = 1;

    public int? DayOfMonth { get; set; }

    public DayOfWeek? DayOfWeek { get; set; }

    public int? Month { get; set; }

    public BusinessDayAdjustment BusinessDayAdjustment { get; set; } = BusinessDayAdjustment.None;

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public static Pillar3aContributionScheduleEditModel FromSchedule(
        Pillar3aContributionSchedule schedule)
    {
        return new Pillar3aContributionScheduleEditModel
        {
            Id = schedule.Id,
            PaymentAccountId = schedule.PaymentAccountId,
            StartDate = schedule.StartDate,
            EndDate = schedule.EndDate,
            Amount = schedule.Amount,
            Currency = schedule.Currency,
            Frequency = schedule.Frequency,
            Interval = schedule.Interval,
            DayOfMonth = schedule.DayOfMonth,
            DayOfWeek = schedule.DayOfWeek,
            Month = schedule.Month,
            BusinessDayAdjustment = schedule.BusinessDayAdjustment,
            IsActive = schedule.IsActive,
            Notes = schedule.Notes
        };
    }

    public Pillar3aContributionSchedule ToSchedule()
    {
        if (PaymentAccountId is null)
        {
            throw new InvalidOperationException("Payment account is required for a Pillar 3a contribution schedule.");
        }

        return new Pillar3aContributionSchedule
        {
            Id = Id,
            PaymentAccountId = PaymentAccountId.Value,
            StartDate = StartDate,
            EndDate = EndDate,
            Amount = Amount,
            Currency = Currency.Trim().ToUpperInvariant(),
            Frequency = Frequency,
            Interval = Interval,
            DayOfMonth = DayOfMonth,
            DayOfWeek = DayOfWeek,
            Month = Month,
            BusinessDayAdjustment = BusinessDayAdjustment,
            IsActive = IsActive,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };
    }
}