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

    /// <summary>
    /// The amount actually posted: nominal, in the money of
    /// <see cref="Date"/>, with any indexation already applied.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// The indexation factor <see cref="Amount"/> already carries -- exactly
    /// <c>1</c> when the source transaction is not indexed. Dividing
    /// <see cref="Amount"/> by it gives the amount as the user typed it, so a UI
    /// can show "CHF 1'000 in 2026 money, CHF 1'486 when it is paid" without
    /// re-deriving the rate.
    /// </summary>
    public decimal IndexationFactor { get; init; } = 1m;

    public string Currency { get; init; } = "CHF";

    public int Priority { get; init; } = 100;

    public string? Category { get; init; }

    public string? Counterparty { get; init; }

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Unknown;

    public string? Notes { get; init; }

    public override string ToString()
        => $"{Date:yyyy-MM-dd}: {Name} {Amount:N2} {Currency}";
}