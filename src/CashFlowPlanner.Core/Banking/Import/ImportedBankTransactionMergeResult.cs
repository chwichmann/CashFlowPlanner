namespace CashFlowPlanner.Core.Banking.Import;

public sealed class ImportedBankTransactionMergeResult
{
    public required ImportedBankStatementBatch Batch { get; init; }

    public IReadOnlyList<ImportedBankTransaction> MergedTransactions { get; init; } = [];

    public IReadOnlyList<ImportedBankTransaction> AddedTransactions { get; init; } = [];

    public IReadOnlyList<ImportedBankTransaction> SkippedDuplicateTransactions { get; init; } = [];

    public int ExistingTransactionCountBeforeMerge { get; init; }

    public int ExistingTransactionCountAfterMerge =>
        MergedTransactions.Count;

    public int AddedCount =>
        AddedTransactions.Count;

    public int SkippedDuplicateCount =>
        SkippedDuplicateTransactions.Count;
}