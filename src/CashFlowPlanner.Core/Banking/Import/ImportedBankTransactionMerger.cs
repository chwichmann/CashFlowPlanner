namespace CashFlowPlanner.Core.Banking.Import;

public sealed class ImportedBankTransactionMerger
{
    public ImportedBankTransactionMergeResult Merge(
        IReadOnlyCollection<ImportedBankTransaction> existingTransactions,
        ImportedBankStatementMappingResult import)
    {
        ArgumentNullException.ThrowIfNull(existingTransactions);
        ArgumentNullException.ThrowIfNull(import);

        var existingByDeduplicationKey = existingTransactions
            .Where(x => !string.IsNullOrWhiteSpace(x.DeduplicationKey))
            .ToDictionary(
                x => x.DeduplicationKey,
                x => x,
                StringComparer.OrdinalIgnoreCase);

        var merged = existingTransactions
            .OrderBy(x => x.ValueDate)
            .ThenBy(x => x.BookingDate)
            .ThenBy(x => x.Description)
            .ToList();

        var added = new List<ImportedBankTransaction>();
        var skipped = new List<ImportedBankTransaction>();

        foreach (var importedTransaction in import.Transactions)
        {
            if (string.IsNullOrWhiteSpace(importedTransaction.DeduplicationKey))
            {
                throw new InvalidOperationException(
                    $"Imported transaction '{importedTransaction.Id}' has no deduplication key.");
            }

            if (existingByDeduplicationKey.ContainsKey(importedTransaction.DeduplicationKey))
            {
                skipped.Add(importedTransaction);
                continue;
            }

            existingByDeduplicationKey[importedTransaction.DeduplicationKey] = importedTransaction;
            merged.Add(importedTransaction);
            added.Add(importedTransaction);
        }

        merged = merged
            .OrderBy(x => x.ValueDate)
            .ThenBy(x => x.BookingDate)
            .ThenBy(x => x.Description)
            .ThenBy(x => x.BankReference)
            .ToList();

        return new ImportedBankTransactionMergeResult
        {
            Batch = import.Batch,
            MergedTransactions = merged,
            AddedTransactions = added,
            SkippedDuplicateTransactions = skipped,
            ExistingTransactionCountBeforeMerge = existingTransactions.Count
        };
    }
}