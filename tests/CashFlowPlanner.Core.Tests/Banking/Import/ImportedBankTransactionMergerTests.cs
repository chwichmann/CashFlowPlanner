using CashFlowPlanner.Core.Banking.Import;
using CashFlowPlanner.Core.Banking.Mt940;

namespace CashFlowPlanner.Core.Tests.Banking.Import;

public sealed class ImportedBankTransactionMergerTests
{
    [Fact]
    public void Merge_AddsNewTransactions()
    {
        var import = CreateImport(
            accountId: Guid.NewGuid(),
            bankReference: "REF-1",
            amount: -40m);

        var merger = new ImportedBankTransactionMerger();

        var result = merger.Merge(
            [],
            import);

        Assert.Equal(0, result.ExistingTransactionCountBeforeMerge);
        Assert.Equal(1, result.ExistingTransactionCountAfterMerge);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(0, result.SkippedDuplicateCount);
        Assert.Single(result.MergedTransactions);
        Assert.Single(result.AddedTransactions);
        Assert.Empty(result.SkippedDuplicateTransactions);
    }

    [Fact]
    public void Merge_SkipsDuplicateTransactions()
    {
        var accountId = Guid.NewGuid();

        var firstImport = CreateImport(
            accountId,
            "REF-1",
            -40m);

        var merger = new ImportedBankTransactionMerger();

        var firstMerge = merger.Merge(
            [],
            firstImport);

        var secondImport = CreateImport(
            accountId,
            "REF-1",
            -40m);

        var secondMerge = merger.Merge(
            firstMerge.MergedTransactions,
            secondImport);

        Assert.Equal(1, secondMerge.ExistingTransactionCountBeforeMerge);
        Assert.Equal(1, secondMerge.ExistingTransactionCountAfterMerge);
        Assert.Equal(0, secondMerge.AddedCount);
        Assert.Equal(1, secondMerge.SkippedDuplicateCount);
        Assert.Single(secondMerge.MergedTransactions);
        Assert.Empty(secondMerge.AddedTransactions);
        Assert.Single(secondMerge.SkippedDuplicateTransactions);
    }

    [Fact]
    public void Merge_AddsSameBankReferenceForDifferentAccount()
    {
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();

        var firstImport = CreateImport(
            accountId1,
            "REF-1",
            -40m);

        var secondImport = CreateImport(
            accountId2,
            "REF-1",
            -40m);

        var merger = new ImportedBankTransactionMerger();

        var firstMerge = merger.Merge(
            [],
            firstImport);

        var secondMerge = merger.Merge(
            firstMerge.MergedTransactions,
            secondImport);

        Assert.Equal(2, secondMerge.ExistingTransactionCountAfterMerge);
        Assert.Equal(1, secondMerge.AddedCount);
        Assert.Equal(0, secondMerge.SkippedDuplicateCount);
    }

    [Fact]
    public void Merge_KeepsExistingTransactionsAndAddsOnlyNewOnes()
    {
        var accountId = Guid.NewGuid();
        var merger = new ImportedBankTransactionMerger();

        var firstImport = CreateImport(
            accountId,
            "REF-1",
            -40m);

        var firstMerge = merger.Merge(
            [],
            firstImport);

        var secondImport = CreateImportWithTwoTransactions(
            accountId,
            duplicateBankReference: "REF-1",
            newBankReference: "REF-2");

        var secondMerge = merger.Merge(
            firstMerge.MergedTransactions,
            secondImport);

        Assert.Equal(1, secondMerge.ExistingTransactionCountBeforeMerge);
        Assert.Equal(2, secondMerge.ExistingTransactionCountAfterMerge);
        Assert.Equal(1, secondMerge.AddedCount);
        Assert.Equal(1, secondMerge.SkippedDuplicateCount);

        Assert.Contains(
            secondMerge.MergedTransactions,
            x => x.BankReference == "REF-1");

        Assert.Contains(
            secondMerge.MergedTransactions,
            x => x.BankReference == "REF-2");
    }

    private static ImportedBankStatementMappingResult CreateImport(
        Guid accountId,
        string bankReference,
        decimal amount)
    {
        var transaction = CreateImportedTransaction(
            accountId,
            bankReference,
            amount);

        var batch = new ImportedBankStatementBatch
        {
            Id = transaction.ImportBatchId,
            AccountId = accountId,
            SourceFormat = "MT940",
            FileFingerprint = Guid.NewGuid().ToString("N"),
            BankAccountIdentifier = "CH230021021010831140E",
            ParsedTransactionCount = 1,
            TransactionNetAmount = amount,
            Currency = "CHF"
        };

        return new ImportedBankStatementMappingResult
        {
            Batch = batch,
            Transactions =
            [
                transaction
            ]
        };
    }

    private static ImportedBankStatementMappingResult CreateImportWithTwoTransactions(
        Guid accountId,
        string duplicateBankReference,
        string newBankReference)
    {
        var batchId = Guid.NewGuid();

        var duplicate = CreateImportedTransaction(
            accountId,
            duplicateBankReference,
            -40m,
            batchId);

        var additional = CreateImportedTransaction(
            accountId,
            newBankReference,
            -10m,
            batchId);

        var batch = new ImportedBankStatementBatch
        {
            Id = batchId,
            AccountId = accountId,
            SourceFormat = "MT940",
            FileFingerprint = Guid.NewGuid().ToString("N"),
            BankAccountIdentifier = "CH230021021010831140E",
            ParsedTransactionCount = 2,
            TransactionNetAmount = -50m,
            Currency = "CHF"
        };

        return new ImportedBankStatementMappingResult
        {
            Batch = batch,
            Transactions =
            [
                duplicate,
                additional
            ]
        };
    }

    private static ImportedBankTransaction CreateImportedTransaction(
        Guid accountId,
        string bankReference,
        decimal amount,
        Guid? batchId = null)
    {
        var transactionWithoutKey = new ImportedBankTransaction
        {
            Id = Guid.NewGuid(),
            ImportBatchId = batchId ?? Guid.NewGuid(),
            AccountId = accountId,
            SourceFormat = "MT940",
            BankAccountIdentifier = "CH230021021010831140E",
            ValueDate = new DateOnly(2026, 1, 5),
            BookingDate = new DateOnly(2026, 1, 5),
            SignedAmount = amount,
            Currency = "CHF",
            TransactionCode = "NMSC",
            Structured86Code = "K70",
            BankReference = bankReference,
            Description = "Test transaction",
            Raw61 = "raw61",
            Raw86 = "raw86"
        };

        return new ImportedBankTransaction
        {
            Id = transactionWithoutKey.Id,
            ImportBatchId = transactionWithoutKey.ImportBatchId,
            AccountId = transactionWithoutKey.AccountId,
            SourceFormat = transactionWithoutKey.SourceFormat,
            BankAccountIdentifier = transactionWithoutKey.BankAccountIdentifier,
            ValueDate = transactionWithoutKey.ValueDate,
            BookingDate = transactionWithoutKey.BookingDate,
            SignedAmount = transactionWithoutKey.SignedAmount,
            Currency = transactionWithoutKey.Currency,
            TransactionCode = transactionWithoutKey.TransactionCode,
            Structured86Code = transactionWithoutKey.Structured86Code,
            BankReference = transactionWithoutKey.BankReference,
            CustomerReference = transactionWithoutKey.CustomerReference,
            SupplementaryDetails = transactionWithoutKey.SupplementaryDetails,
            Description = transactionWithoutKey.Description,
            Raw61 = transactionWithoutKey.Raw61,
            Raw86 = transactionWithoutKey.Raw86,
            DeduplicationKey = ImportedBankTransactionDedupKeyBuilder.Build(transactionWithoutKey),
            MatchStatus = transactionWithoutKey.MatchStatus,
            ImportedUtc = transactionWithoutKey.ImportedUtc
        };
    }
}