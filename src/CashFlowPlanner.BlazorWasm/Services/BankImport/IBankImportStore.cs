using CashFlowPlanner.Core.Banking.Import;

namespace CashFlowPlanner.BlazorWasm.Services.BankImport;

public interface IBankImportStore
{
    Task InitializeAsync();

    Task<IReadOnlyList<ImportedBankTransaction>> GetAllTransactionsAsync();

    Task<IReadOnlyList<ImportedBankTransaction>> GetTransactionsForAccountAsync(Guid accountId);

    Task<IReadOnlyList<ImportedBankStatementBatch>> GetBatchesAsync();

    Task ApplyImportAsync(ImportedBankTransactionMergeResult mergeResult);

    Task ClearAllAsync();

    Task ClearAccountAsync(Guid accountId);
}