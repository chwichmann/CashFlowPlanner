namespace CashFlowPlanner.Core;

public sealed class BankOffDay
{
    public DateOnly Date { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Note { get; init; }

    public void Validate()
    {
        if (Date == default)
        {
            throw new InvalidOperationException("Bank off-day date is required.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Bank off-day name is required.");
        }
    }
}