using CashFlowPlanner.Core;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class BankOffDayEditModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string Name { get; set; } = string.Empty;

    public string? Note { get; set; }

    public static BankOffDayEditModel FromModel(BankOffDay model)
    {
        return new BankOffDayEditModel
        {
            Date = model.Date,
            Name = model.Name,
            Note = model.Note
        };
    }

    public BankOffDay ToModel()
    {
        return new BankOffDay
        {
            Date = Date,
            Name = Name.Trim(),
            Note = string.IsNullOrWhiteSpace(Note)
                ? null
                : Note.Trim()
        };
    }
}