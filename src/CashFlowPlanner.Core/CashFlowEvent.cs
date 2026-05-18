namespace CashFlowPlanner.Core;

public sealed class CashFlowEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SourceTransactionId { get; init; }

    public required string Name { get; init; }

    public DateOnly Date { get; init; }

    public TransactionKind Kind { get; init; }

    public Guid? FromAccountId { get; init; }

    public Guid? ToAccountId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "CHF";

    public int Priority { get; init; } = 100;

    public string? Category { get; init; }

    public string? Counterparty { get; init; }

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Unknown;

    public string? Notes { get; init; }

    public override string ToString()
        => $"{Date:yyyy-MM-dd}: {Name} {Amount:N2} {Currency}";
}