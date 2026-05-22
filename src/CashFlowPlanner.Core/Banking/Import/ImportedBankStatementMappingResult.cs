namespace CashFlowPlanner.Core.Banking.Import;

public sealed class ImportedBankStatementMappingResult
{
    public required ImportedBankStatementBatch Batch { get; init; }

    public IReadOnlyList<ImportedBankTransaction> Transactions { get; init; } = [];
}