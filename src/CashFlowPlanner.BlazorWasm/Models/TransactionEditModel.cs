using CashFlowPlanner.Core;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class TransactionEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public TransactionKind Kind { get; set; } = TransactionKind.ExternalExpense;

    public Guid? FromAccountId { get; set; }

    public Guid? ToAccountId { get; set; }

    public Guid? IncomePersonId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CHF";

    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Monthly;

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? EndDate { get; set; }

    public int Interval { get; set; } = 1;

    public int? DayOfMonth { get; set; }

    public DayOfWeek? DayOfWeek { get; set; }

    public int? Month { get; set; }

    public BusinessDayAdjustment BusinessDayAdjustment { get; set; } = BusinessDayAdjustment.None;

    public string? Category { get; set; }

    public string? Counterparty { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Unknown;

    public int Priority { get; set; } = 100;

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public static TransactionEditModel FromTransaction(TransactionDefinition transaction)
    {
        return new TransactionEditModel
        {
            Id = transaction.Id,
            Name = transaction.Name,
            Kind = transaction.Kind,
            FromAccountId = transaction.FromAccountId,
            ToAccountId = transaction.ToAccountId,
            IncomePersonId = transaction.IncomePersonId,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Frequency = transaction.Schedule.Frequency,
            StartDate = transaction.Schedule.StartDate,
            EndDate = transaction.Schedule.EndDate,
            Interval = transaction.Schedule.Interval,
            DayOfMonth = transaction.Schedule.DayOfMonth,
            DayOfWeek = transaction.Schedule.DayOfWeek,
            Month = transaction.Schedule.Month,
            BusinessDayAdjustment = transaction.Schedule.BusinessDayAdjustment,
            Category = transaction.Category,
            Counterparty = transaction.Counterparty,
            PaymentMethod = transaction.PaymentMethod,
            Priority = transaction.Priority,
            IsActive = transaction.IsActive,
            Notes = transaction.Notes
        };
    }

    public TransactionDefinition ToTransaction()
    {
        return new TransactionDefinition
        {
            Id = Id,
            Name = Name.Trim(),
            Kind = Kind,
            FromAccountId = FromAccountId,
            ToAccountId = ToAccountId,
            IncomePersonId = IncomePersonId,
            Amount = Amount,
            Currency = Currency.Trim().ToUpperInvariant(),
            Schedule = new Schedule
            {
                Frequency = Frequency,
                StartDate = StartDate,
                EndDate = EndDate,
                Interval = Interval,
                DayOfMonth = DayOfMonth,
                DayOfWeek = DayOfWeek,
                Month = Month,
                BusinessDayAdjustment = BusinessDayAdjustment
            },
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
            Counterparty = string.IsNullOrWhiteSpace(Counterparty) ? null : Counterparty.Trim(),
            PaymentMethod = PaymentMethod,
            Priority = Priority,
            IsActive = IsActive,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };
    }
}