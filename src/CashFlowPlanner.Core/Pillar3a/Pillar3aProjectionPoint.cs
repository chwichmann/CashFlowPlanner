namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aProjectionPoint
{
    public DateOnly Date { get; init; }

    public decimal Contributions { get; init; }

    public decimal Growth { get; init; }

    public decimal Withdrawals { get; init; }

    public decimal Value { get; init; }

    public string Currency { get; init; } = "CHF";
}
