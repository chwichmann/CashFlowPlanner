using CashFlowPlanner.Core.Banking.Mt940;

namespace CashFlowPlanner.Core.Banking.Import;

public sealed class ImportedBankStatementMapper
{
    public ImportedBankStatementMappingResult MapFromMt940(
        Mt940Statement statement,
        Guid accountId,
        string fileFingerprint,
        string? fileName = null,
        DateTime? importedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var importTime = importedUtc ?? DateTime.UtcNow;
        var batchId = Guid.NewGuid();

        var transactions = statement.Transactions
            .Select(transaction => MapTransaction(
                statement,
                transaction,
                accountId,
                batchId,
                importTime))
            .ToList();

        var batch = new ImportedBankStatementBatch
        {
            Id = batchId,
            AccountId = accountId,
            SourceFormat = "MT940",
            FileName = fileName,
            FileFingerprint = fileFingerprint,
            BankAccountIdentifier = statement.AccountIdentifier,
            TransactionReference = statement.TransactionReference,
            StatementNumber = statement.StatementNumber,
            OpeningBalanceDate = statement.OpeningBalance?.Date,
            OpeningBalance = statement.OpeningBalance?.Amount,
            ClosingBalanceDate = statement.ClosingBalance?.Date,
            ClosingBalance = statement.ClosingBalance?.Amount,
            Currency =
                statement.ClosingBalance?.Currency
                ?? statement.OpeningBalance?.Currency
                ?? "CHF",
            FirstTransactionDate = transactions.Count == 0
                ? null
                : transactions.Min(x => x.ValueDate),
            LastTransactionDate = transactions.Count == 0
                ? null
                : transactions.Max(x => x.ValueDate),
            ParsedTransactionCount = transactions.Count,
            TransactionNetAmount = transactions.Sum(x => x.SignedAmount),
            ReconciliationAvailable = statement.Reconciliation.IsAvailable,
            ReconciliationBalanced = statement.Reconciliation.IsBalanced,
            ReconciliationDifference = statement.Reconciliation.Difference,
            ImportedUtc = importTime
        };

        return new ImportedBankStatementMappingResult
        {
            Batch = batch,
            Transactions = transactions
        };
    }

    private static ImportedBankTransaction MapTransaction(
        Mt940Statement statement,
        Mt940Transaction transaction,
        Guid accountId,
        Guid batchId,
        DateTime importedUtc)
    {
        var importedTransaction = new ImportedBankTransaction
        {
            Id = Guid.NewGuid(),
            ImportBatchId = batchId,
            AccountId = accountId,
            SourceFormat = "MT940",
            BankAccountIdentifier = statement.AccountIdentifier ?? string.Empty,
            ValueDate = transaction.ValueDate,
            BookingDate = transaction.BookingDate,
            SignedAmount = transaction.SignedAmount,
            Currency = transaction.Currency,
            TransactionCode = transaction.TransactionCode,
            Structured86Code = transaction.Structured86Code,
            BankReference = transaction.BankReference,
            CustomerReference = transaction.CustomerReference,
            SupplementaryDetails = transaction.SupplementaryDetails,
            Description = transaction.Description,
            Raw61 = transaction.Raw61,
            Raw86 = transaction.Raw86,
            ImportedUtc = importedUtc
        };

        return importedTransaction.withDeduplicationKey();
    }
}

internal static class ImportedBankTransactionExtensions
{
    public static ImportedBankTransaction withDeduplicationKey(
        this ImportedBankTransaction transaction)
    {
        return new ImportedBankTransaction
        {
            Id = transaction.Id,
            ImportBatchId = transaction.ImportBatchId,
            AccountId = transaction.AccountId,
            SourceFormat = transaction.SourceFormat,
            BankAccountIdentifier = transaction.BankAccountIdentifier,
            ValueDate = transaction.ValueDate,
            BookingDate = transaction.BookingDate,
            SignedAmount = transaction.SignedAmount,
            Currency = transaction.Currency,
            TransactionCode = transaction.TransactionCode,
            Structured86Code = transaction.Structured86Code,
            BankReference = transaction.BankReference,
            CustomerReference = transaction.CustomerReference,
            SupplementaryDetails = transaction.SupplementaryDetails,
            Description = transaction.Description,
            Raw61 = transaction.Raw61,
            Raw86 = transaction.Raw86,
            DeduplicationKey = ImportedBankTransactionDedupKeyBuilder.Build(transaction),
            MatchedTransactionDefinitionId = transaction.MatchedTransactionDefinitionId,
            MatchStatus = transaction.MatchStatus,
            ImportedUtc = transaction.ImportedUtc
        };
    }
}