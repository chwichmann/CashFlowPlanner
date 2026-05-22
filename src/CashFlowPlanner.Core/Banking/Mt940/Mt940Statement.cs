namespace CashFlowPlanner.Core.Banking.Mt940;

public sealed class Mt940Statement
{
    public string? TransactionReference { get; init; }

    public string? AccountIdentifier { get; init; }

    public string? StatementNumber { get; init; }

    public Mt940Balance? OpeningBalance { get; init; }

    public Mt940Balance? ClosingBalance { get; init; }

    public IReadOnlyList<Mt940Transaction> Transactions { get; init; } = [];

    public Mt940ReconciliationResult Reconciliation { get; init; } =
        Mt940ReconciliationResult.NotAvailable();

    public string RawBody { get; init; } = string.Empty;
}