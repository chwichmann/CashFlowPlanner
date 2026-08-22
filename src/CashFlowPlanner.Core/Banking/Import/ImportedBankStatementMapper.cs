using System.Globalization;
using CashFlowPlanner.Core.Banking.Camt;
using CashFlowPlanner.Core.Banking.Csv;
using CashFlowPlanner.Core.Banking.Mt940;

namespace CashFlowPlanner.Core.Banking.Import;

public sealed class ImportedBankStatementMapper
{
    /// <summary>
    /// How many items of an internal batch booking are spelled out in the description before it
    /// is cut short. The import store is localStorage-backed and shared with the plan, so an
    /// unbounded itemisation is a real risk to the user's data, not just to readability.
    /// </summary>
    private const int MaxDescribedBatchItems = 10;

    /// <summary>
    /// Mirrors <see cref="BankStatementImportService.CsvSourceFormat"/>. Kept as a literal here
    /// for the same reason "MT940" and "CAMT053" are: <see cref="ImportedBankStatementBatch"/>
    /// persists it as a plain string into the user's browser storage, and a value that moved
    /// would orphan every batch already stored under the old one.
    /// </summary>
    private const string CsvSourceFormat = "CSV";

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

    /// <summary>
    /// Maps <b>one</b> CAMT.053 statement to one import batch.
    ///
    /// <para>
    /// A camt.053 file holds <c>1..n</c> statements, one per account, and
    /// <see cref="ImportedBankStatementBatch"/> has a single <c>AccountId</c>. The caller
    /// therefore calls this once per <see cref="Camt053Statement"/> with the account that
    /// statement's IBAN matched; flattening several statements into one batch would mix
    /// accounts and reconcile them against the wrong balances.
    /// </para>
    ///
    /// <para>
    /// One <see cref="ImportedBankTransaction"/> is produced per <c>Ntry</c> - never per
    /// <c>TxDtls</c>. That is what makes double-counting an internal batch booking structurally
    /// impossible rather than merely avoided: the entry amount is the booked amount, and the
    /// details only supply text and references.
    /// </para>
    /// </summary>
    public ImportedBankStatementMappingResult MapFromCamt053(
        Camt053Statement statement,
        Guid accountId,
        string fileFingerprint,
        string? fileName = null,
        DateTime? importedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var importTime = importedUtc ?? DateTime.UtcNow;
        var batchId = Guid.NewGuid();

        var transactions = statement.Entries
            .Select(entry => MapEntry(
                statement,
                entry,
                accountId,
                batchId,
                importTime))
            .ToList();

        var batch = new ImportedBankStatementBatch
        {
            Id = batchId,
            AccountId = accountId,
            SourceFormat = "CAMT053",
            FileName = fileName,
            FileFingerprint = fileFingerprint,
            BankAccountIdentifier = statement.AccountIdentifier,
            // Stmt/Id plus the sequence number below identify this statement within the file,
            // which is what per-statement duplicate detection needs - the file fingerprint only
            // covers the file as a whole.
            TransactionReference = statement.Id,
            StatementNumber =
                statement.ElectronicSequenceNumber
                ?? statement.LegalSequenceNumber,
            OpeningBalanceDate = statement.OpeningBalance?.Date,
            OpeningBalance = statement.OpeningBalance?.SignedAmount,
            ClosingBalanceDate = statement.ClosingBalance?.Date,
            ClosingBalance = statement.ClosingBalance?.SignedAmount,
            Currency =
                statement.ClosingBalance?.Currency
                ?? statement.OpeningBalance?.Currency
                ?? statement.Currency,
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

    /// <summary>
    /// Maps a parsed CSV export to one import batch.
    ///
    /// <para>
    /// Two things are deliberately different from the MT940 and camt.053 paths, and both are
    /// about not pretending to know more than the file says.
    /// </para>
    ///
    /// <para>
    /// <b>No bank reference.</b> A reference column, where one exists, goes to
    /// <see cref="ImportedBankTransaction.CustomerReference"/> and never to
    /// <see cref="ImportedBankTransaction.BankReference"/>, which is the primary deduplication
    /// tier. That looks like throwing away a perfectly good key, and it is not: a CSV reference
    /// column usually holds the QR or ESR reference of the payment, and a standing order carries
    /// the <i>same</i> reference every single month. Keying on it would make February's rent a
    /// duplicate of January's and drop it. So every CSV transaction goes through the content-hash
    /// fallback tier, with the occurrence rank that
    /// <see cref="ImportedBankTransactionDedupKeyBuilder.Build(ImportedBankTransaction, int)"/>
    /// applies keeping genuinely identical rows apart.
    /// </para>
    ///
    /// <para>
    /// <b>No fabricated balances.</b> Opening and closing balances are set only when the export
    /// actually carried a running-balance column and it reconciled; otherwise they stay null, and
    /// with them the suggested balance update. A CSV file with no balance column tells us nothing
    /// about the account's balance, and inventing one from the transactions would overwrite a
    /// figure the user maintains by hand with a number derived from an arbitrary starting point.
    /// </para>
    /// </summary>
    public ImportedBankStatementMappingResult MapFromCsv(
        CsvStatementFile file,
        Guid accountId,
        string fileFingerprint,
        string? fileName = null,
        DateTime? importedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        var importTime = importedUtc ?? DateTime.UtcNow;
        var batchId = Guid.NewGuid();

        var transactions = MapCsvRows(file, accountId, batchId, importTime);

        var reconciliation = file.Reconciliation;

        var batch = new ImportedBankStatementBatch
        {
            Id = batchId,
            AccountId = accountId,
            SourceFormat = CsvSourceFormat,
            FileName = fileName,
            FileFingerprint = fileFingerprint,
            BankAccountIdentifier = file.AccountIdentifier,
            // A CSV export has no statement identity of its own - no :20:, no Stmt/Id. The file
            // fingerprint above is the only thing that identifies this particular download, and
            // it is already there.
            TransactionReference = null,
            StatementNumber = null,
            OpeningBalanceDate = reconciliation.IsAvailable ? reconciliation.OpeningBalanceDate : null,
            OpeningBalance = reconciliation.IsAvailable ? reconciliation.OpeningBalance : null,
            ClosingBalanceDate = reconciliation.IsAvailable ? reconciliation.ClosingBalanceDate : null,
            ClosingBalance = reconciliation.IsAvailable ? reconciliation.ClosingBalance : null,
            Currency = file.Currency,
            FirstTransactionDate = transactions.Count == 0
                ? null
                : transactions.Min(x => x.ValueDate),
            LastTransactionDate = transactions.Count == 0
                ? null
                : transactions.Max(x => x.ValueDate),
            ParsedTransactionCount = transactions.Count,
            TransactionNetAmount = transactions.Sum(x => x.SignedAmount),
            ReconciliationAvailable = reconciliation.IsAvailable,
            ReconciliationBalanced = reconciliation.IsBalanced,
            ReconciliationDifference = reconciliation.Difference,
            ImportedUtc = importTime
        };

        return new ImportedBankStatementMappingResult
        {
            Batch = batch,
            Transactions = transactions
        };
    }

    /// <summary>
    /// Maps the rows in file order, ranking identical ones as it goes.
    ///
    /// <para>
    /// The rank has to be assigned here rather than inside the key builder because it is a
    /// property of the <i>statement</i>, not of the transaction: only something that sees all the
    /// rows at once can know that this 4.50 is the second identical 4.50 of the day.
    /// </para>
    /// </summary>
    private static IReadOnlyList<ImportedBankTransaction> MapCsvRows(
        CsvStatementFile file,
        Guid accountId,
        Guid batchId,
        DateTime importTime)
    {
        var occurrencesByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var transactions = new List<ImportedBankTransaction>(file.Rows.Count);

        foreach (var row in file.Rows)
        {
            var transaction = MapCsvRow(file, row, accountId, batchId, importTime);

            var baseKey = ImportedBankTransactionDedupKeyBuilder.Build(transaction);

            occurrencesByKey[baseKey] = occurrencesByKey.TryGetValue(baseKey, out var seen)
                ? seen + 1
                : 1;

            transactions.Add(
                transaction.withDeduplicationKey(occurrencesByKey[baseKey]));
        }

        return transactions;
    }

    private static ImportedBankTransaction MapCsvRow(
        CsvStatementFile file,
        CsvStatementRow row,
        Guid accountId,
        Guid batchId,
        DateTime importTime)
    {
        return new ImportedBankTransaction
        {
            Id = Guid.NewGuid(),
            ImportBatchId = batchId,
            AccountId = accountId,
            SourceFormat = CsvSourceFormat,
            BankAccountIdentifier = file.AccountIdentifier ?? string.Empty,
            ValueDate = row.EffectiveDate,
            // Only when the file has both columns. Repeating the value date here would make the
            // two look confirmed by the export when only one of them was in it.
            BookingDate = row.ValueDate is null ? null : row.BookingDate,
            SignedAmount = row.SignedAmount,
            Currency = row.Currency,
            // CSV has no equivalent of the MT940 transaction code or camt's BkTxCd. Empty rather
            // than a placeholder, because the field feeds the deduplication hash and a made-up
            // constant there would be noise in every key.
            TransactionCode = string.Empty,
            Structured86Code = null,
            BankReference = null,
            CustomerReference = row.Reference,
            SupplementaryDetails = row.Counterparty,
            Description = BuildCsvDescription(row),
            // The row as it stood in the file. A CSV line is short - a hundred characters or so,
            // comparable to the MT940 :61:/:86: pair already stored here - and it is what lets a
            // user challenge an amount six months later.
            Raw61 = row.RawText,
            Raw86 = null,
            ImportedUtc = importTime
        };
    }

    /// <summary>
    /// Builds the description the same way the camt path does: the counterparty first, because
    /// that is what a person recognises, then what the payment was for - and never the
    /// counterparty twice when the description already opens with it.
    /// </summary>
    private static string BuildCsvDescription(CsvStatementRow row)
    {
        var purpose = row.Description;
        var counterparty = row.Counterparty;

        if (string.IsNullOrWhiteSpace(counterparty))
        {
            return string.IsNullOrWhiteSpace(purpose)
                ? string.Empty
                : NormalizeText(purpose);
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            return NormalizeText(counterparty);
        }

        return purpose.Contains(counterparty, StringComparison.OrdinalIgnoreCase)
            ? NormalizeText(purpose)
            : NormalizeText($"{counterparty} {purpose}");
    }

    private static ImportedBankTransaction MapEntry(
        Camt053Statement statement,
        Camt053Entry entry,
        Guid accountId,
        Guid batchId,
        DateTime importedUtc)
    {
        // Enrichment is only unambiguous when the entry describes a single transaction. For an
        // internal batch booking the details belong to different counterparties, so per-item
        // references must not be promoted to the booking.
        var singleDetail = entry.Details.Count == 1
            ? entry.Details[0]
            : null;

        var importedTransaction = new ImportedBankTransaction
        {
            Id = Guid.NewGuid(),
            ImportBatchId = batchId,
            AccountId = accountId,
            SourceFormat = "CAMT053",
            BankAccountIdentifier = statement.AccountIdentifier ?? string.Empty,
            ValueDate = entry.ValueDate,
            BookingDate = entry.BookingDate,
            // Sign already applied from CdtDbtInd - camt amounts are unsigned.
            SignedAmount = entry.SignedAmount,
            Currency = entry.Currency,
            // BkTxCd (Domn/Fmly/SubFmlyCd) is the format's categorisation field and the closest
            // thing to the MT940 four-character transaction code this field held before.
            TransactionCode = entry.BankTransactionCode,
            // Structured86Code carried the MT940 :86: structured code. Its CAMT.053 equivalent is
            // the *type* of the structured creditor reference - QRR for a QR-IBAN reference, SCOR
            // for an ISO 11649 RF reference, "ISR Reference" for LSV+/BDD - because that is what
            // tells a consumer how to interpret the reference in SupplementaryDetails.
            Structured86Code = singleDetail?.CreditorReferenceType,
            // AcctSvcrRef is the bank's own booking reference and the field the Swiss IG names
            // for duplicate checking. It is the primary tier of the deduplication key.
            BankReference =
                entry.AccountServicerReference
                ?? singleDetail?.AccountServicerReference
                ?? entry.EntryReference,
            // EndToEndId only exists for customer-initiated payments and is often NOTPROVIDED,
            // which the parser already normalises away - so it enriches, it never keys.
            CustomerReference = singleDetail?.EndToEndId,
            SupplementaryDetails = singleDetail?.CreditorReference,
            Description = BuildDescription(entry),
            // Raw61/Raw86 are MT940 record names and there is no MT940 record here. The entry's
            // XML is kept on Camt053Entry.RawXml for diagnostics but deliberately not persisted:
            // it is 1-2 KB per transaction, the import store lives in localStorage alongside the
            // plan (~5 MB in total), and every field the app reads is already mapped above.
            Raw61 = string.Empty,
            Raw86 = null,
            ImportedUtc = importedUtc
        };

        return importedTransaction.withDeduplicationKey();
    }

    /// <summary>
    /// Builds the human-readable description, preferring what a person would actually recognise:
    /// the counterparty and the payment purpose, then the bank's own entry line.
    /// </summary>
    private static string BuildDescription(Camt053Entry entry)
    {
        if (entry.Details.Count > 1)
        {
            return BuildBatchDescription(entry);
        }

        var detail = entry.Details.Count == 1
            ? entry.Details[0]
            : null;

        // The payment purpose, in order of specificity, ending at the bank's own entry line.
        var purpose =
            detail?.UnstructuredRemittanceInformation
            ?? detail?.AdditionalTransactionInformation
            ?? entry.AdditionalEntryInformation;

        var counterparty = detail?.CounterpartyName;

        if (counterparty is null)
        {
            return purpose is null
                ? string.Empty
                : NormalizeText(purpose);
        }

        if (purpose is null)
        {
            return NormalizeText(counterparty);
        }

        // AddtlNtryInf frequently already opens with the counterparty name; prefixing it again
        // would read "Example Employer AG Example Employer AG Salary January 2026".
        return purpose.Contains(counterparty, StringComparison.OrdinalIgnoreCase)
            ? NormalizeText(purpose)
            : NormalizeText($"{counterparty} {purpose}");
    }

    private static string BuildBatchDescription(Camt053Entry entry)
    {
        var parts = new List<string>();

        if (entry.AdditionalEntryInformation is not null)
        {
            parts.Add(entry.AdditionalEntryInformation);
        }

        foreach (var detail in entry.Details.Take(MaxDescribedBatchItems))
        {
            var itemParts = new[]
                {
                    detail.CounterpartyName,
                    detail.UnstructuredRemittanceInformation
                    ?? detail.AdditionalTransactionInformation
                    ?? detail.CreditorReference,
                    detail.Amount?.ToString("0.00", CultureInfo.InvariantCulture)
                }
                .Where(x => !string.IsNullOrWhiteSpace(x));

            var item = string.Join(" ", itemParts);

            if (!string.IsNullOrWhiteSpace(item))
            {
                parts.Add(item);
            }
        }

        if (entry.Details.Count > MaxDescribedBatchItems)
        {
            parts.Add(
                $"(+{entry.Details.Count - MaxDescribedBatchItems} more)");
        }

        return NormalizeText(string.Join(" | ", parts));
    }

    private static string NormalizeText(string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
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
        return transaction.withDeduplicationKey(occurrence: 1);
    }

    /// <summary>
    /// <paramref name="occurrence"/> is the transaction's rank among identical ones in the same
    /// statement; 1 - the only value the MT940 and camt.053 paths ever pass - produces exactly
    /// the key those formats produced before.
    /// </summary>
    public static ImportedBankTransaction withDeduplicationKey(
        this ImportedBankTransaction transaction,
        int occurrence)
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
            DeduplicationKey = ImportedBankTransactionDedupKeyBuilder.Build(transaction, occurrence),
            MatchedTransactionDefinitionId = transaction.MatchedTransactionDefinitionId,
            MatchStatus = transaction.MatchStatus,
            ImportedUtc = transaction.ImportedUtc
        };
    }
}