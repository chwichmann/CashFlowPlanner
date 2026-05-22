namespace CashFlowPlanner.Core.Banking.Import;

public sealed class BankStatementImportPreview
{
    public string SourceFormat { get; init; } = "MT940";

    public string? FileName { get; init; }

    public string FileFingerprint { get; init; } = string.Empty;

    public string? BankAccountIdentifier { get; init; }

    public string? TransactionReference { get; init; }

    public string? StatementNumber { get; init; }

    public DateOnly? OpeningBalanceDate { get; init; }

    public decimal? OpeningBalance { get; init; }

    public DateOnly? ClosingBalanceDate { get; init; }

    public decimal? ClosingBalance { get; init; }

    public string Currency { get; init; } = "CHF";

    public DateOnly? FirstTransactionDate { get; init; }

    public DateOnly? LastTransactionDate { get; init; }

    public int ParsedTransactionCount { get; init; }

    public decimal TransactionNetAmount { get; init; }

    public bool ReconciliationAvailable { get; init; }

    public bool ReconciliationBalanced { get; init; }

    public decimal? ReconciliationDifference { get; init; }
}