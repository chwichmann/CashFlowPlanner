namespace CashFlowPlanner.Core.Banking.Import;

public sealed class BankStatementSuggestedBalanceUpdate
{
    public Guid AccountId { get; init; }

    public decimal Balance { get; init; }

    public string Currency { get; init; } = "CHF";

    public DateOnly BalanceDate { get; init; }

    public DateOnly? ClosingBalanceDateFromFile { get; init; }

    public DateOnly? LastTransactionDate { get; init; }

    public bool ClosingBalanceDateLooksSuspicious { get; init; }

    public string? Reason { get; init; }
}