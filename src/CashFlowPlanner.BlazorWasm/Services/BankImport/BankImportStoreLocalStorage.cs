using System.Text.Json;
using CashFlowPlanner.Core.Banking.Import;
using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services.BankImport;

public sealed class BankImportStoreLocalStorage : IBankImportStore
{
    private readonly IJSRuntime _js;

    private BankImportStoreState _state = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly IWorkingCopyCipher _cipher;

    public BankImportStoreLocalStorage(IJSRuntime js, IWorkingCopyCipher cipher)
    {
        _js = js;
        _cipher = cipher;
    }

    public async Task InitializeAsync()
    {
        var stored = await _js.InvokeAsync<string?>(
            "localStorage.getItem",
            LocalStorageKeys.BankImportStore);

        // Imported statements carry counterparties, amounts and account references - the same
        // private data as the plan, and it was sitting in the same greppable profile file.
        // Plaintext written by an older build is read as-is and rewritten encrypted on the next
        // save, so nobody loses a reconciled import to this change.
        var json = await _cipher.UnprotectAsync(stored);

        if (string.IsNullOrWhiteSpace(json))
        {
            _state = new BankImportStoreState();
            return;
        }

        _state = JsonSerializer.Deserialize<BankImportStoreState>(
                     json,
                     JsonOptions)
                 ?? new BankImportStoreState();
    }

    public Task<IReadOnlyList<ImportedBankTransaction>> GetAllTransactionsAsync()
    {
        return Task.FromResult(
            (IReadOnlyList<ImportedBankTransaction>)_state.Transactions);
    }

    public Task<IReadOnlyList<ImportedBankTransaction>> GetTransactionsForAccountAsync(Guid accountId)
    {
        var result = _state.Transactions
            .Where(x => x.AccountId == accountId)
            .ToList();

        return Task.FromResult(
            (IReadOnlyList<ImportedBankTransaction>)result);
    }

    public Task<IReadOnlyList<ImportedBankStatementBatch>> GetBatchesAsync()
    {
        return Task.FromResult(
            (IReadOnlyList<ImportedBankStatementBatch>)_state.Batches);
    }

    public async Task ApplyImportAsync(
        ImportedBankTransactionMergeResult mergeResult)
    {
        ArgumentNullException.ThrowIfNull(mergeResult);

        // Replace all transactions with merged result
        var previous = _state;

        _state = new BankImportStoreState
        {
            Batches = _state.Batches
                .Concat(new[] { mergeResult.Batch })
                .ToList(),

            Transactions = mergeResult.MergedTransactions.ToList(),

            LastUpdatedUtc = DateTime.UtcNow
        };

        await SaveAsync(previous);
    }

    public async Task ClearAllAsync()
    {
        var previous = _state;

        _state = new BankImportStoreState();

        await SaveAsync(previous);
    }

    public async Task ClearAccountAsync(Guid accountId)
    {
        var previous = _state;

        _state = new BankImportStoreState
        {
            Batches = _state.Batches
                .Where(x => x.AccountId != accountId)
                .ToList(),

            Transactions = _state.Transactions
                .Where(x => x.AccountId != accountId)
                .ToList(),

            LastUpdatedUtc = DateTime.UtcNow
        };

        await SaveAsync(previous);
    }

    /// <summary>
    /// Writes the store, and puts <paramref name="previous"/> back if the browser refuses.
    /// <para>
    /// Goes through the <c>window.cashFlowPlanner</c> shim rather than calling
    /// <c>localStorage.setItem</c> directly, because a direct call throws a DOMException on a
    /// full quota and the shim reports it instead. Without the rollback the session would go on
    /// showing transactions as imported that are not stored anywhere, and they would vanish on
    /// the next reload - which is the shape of P1b, one key over.
    /// </para>
    /// </summary>
    private async Task SaveAsync(BankImportStoreState previous)
    {
        var json = JsonSerializer.Serialize(
            _state,
            JsonOptions);

        var stored = await _cipher.ProtectAsync(json);

        var written = await _js.InvokeAsync<bool>(
            "cashFlowPlanner.setLocalStorageItem",
            LocalStorageKeys.BankImportStore,
            stored);

        if (written)
        {
            return;
        }

        _state = previous;

        var error = await _js.InvokeAsync<BrowserStorageError?>(
            "cashFlowPlanner.getLastStorageError");

        throw new BankImportStorageException(
            error?.IsQuotaExceeded ?? false,
            error?.Message);
    }
}