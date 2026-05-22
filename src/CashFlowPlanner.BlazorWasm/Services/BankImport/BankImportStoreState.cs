using CashFlowPlanner.Core.Banking.Import;

namespace CashFlowPlanner.BlazorWasm.Services.BankImport;

public sealed class BankImportStoreState
{
    public List<ImportedBankStatementBatch> Batches { get; init; } = [];

    public List<ImportedBankTransaction> Transactions { get; init; } = [];

    public DateTime LastUpdatedUtc { get; init; } = DateTime.UtcNow;
}
