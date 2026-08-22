using System.Text;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Banking.Camt;
using CashFlowPlanner.Core.Banking.Mt940;

namespace CashFlowPlanner.Core.Banking.Import;

public sealed class BankStatementImportService
{
    public const string Mt940SourceFormat = "MT940";

    public const string Camt053SourceFormat = "CAMT053";

    private readonly Mt940Parser _mt940Parser;
    private readonly Camt053Parser _camt053Parser;
    private readonly ImportedBankStatementMapper _mapper;
    private readonly ImportedBankTransactionMerger _merger;

    public BankStatementImportService()
        : this(
            new Mt940Parser(),
            new Camt053Parser(),
            new ImportedBankStatementMapper(),
            new ImportedBankTransactionMerger())
    {
    }

    public BankStatementImportService(
        Mt940Parser mt940Parser,
        Camt053Parser camt053Parser,
        ImportedBankStatementMapper mapper,
        ImportedBankTransactionMerger merger)
    {
        _mt940Parser = mt940Parser;
        _camt053Parser = camt053Parser;
        _mapper = mapper;
        _merger = merger;
    }

    /// <summary>
    /// Imports a bank statement file, detecting the format from its <b>content</b>.
    ///
    /// <para>
    /// Returns one result per statement. MT940 always yields exactly one; a camt.053 file yields
    /// one per <c>Stmt</c>, because <c>Stmt</c> is <c>1..n</c> and a combined export carries
    /// several accounts. Callers must render every result - an unmatched statement that is
    /// dropped from the list is an account whose transactions silently vanished.
    /// </para>
    /// </summary>
    public IReadOnlyList<BankStatementImportResult> Import(
        BankStatementImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FileBytes);

        if (request.FileBytes.Length == 0)
        {
            throw new InvalidOperationException("The imported bank statement file is empty.");
        }

        return DetectSourceFormat(request.FileBytes) == Camt053SourceFormat
            ? ImportCamt053(request)
            : [ImportMt940(request)];
    }

    /// <summary>
    /// Decides whether a file is CAMT.053 or MT940 by looking at the content, not the extension.
    ///
    /// <para>
    /// Extensions are unreliable here: banks export camt as <c>.xml</c>, <c>.txt</c> or inside a
    /// <c>.zip</c>, and users rename files. The content is unambiguous - an XML document with a
    /// <c>BkToCstmrStmt</c> element is camt.053 and nothing else is.
    /// </para>
    /// </summary>
    public static string DetectSourceFormat(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        return Camt053Parser.LooksLikeCamt053(ReadHead(fileBytes))
            ? Camt053SourceFormat
            : Mt940SourceFormat;
    }

    /// <summary>
    /// Decodes the first few kilobytes for format sniffing only.
    ///
    /// Deliberately lossy: the real decode happens in the parser, which honours the XML
    /// declaration. This only has to find an ASCII marker, and must never throw on an
    /// unexpected encoding.
    /// </summary>
    private static string ReadHead(byte[] fileBytes)
    {
        const int HeadLength = 4096;

        var length = Math.Min(fileBytes.Length, HeadLength);

        return new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: false)
            .GetString(fileBytes, 0, length);
    }

    /// <summary>
    /// Imports every statement in a CAMT.053 file, one import batch per <c>Stmt</c>.
    ///
    /// <para>
    /// <b>Unmatched statements are skipped, not fatal.</b> A household holding four accounts at
    /// one bank commonly has two of them in the plan; failing the whole file would make the
    /// combined export unusable and teach the user to avoid it. Every statement still appears in
    /// the returned list with its IBAN and <see cref="BankStatementImportResult.RequiresAccountSelection"/>
    /// set, so nothing disappears quietly, and re-importing the same file after adding the
    /// missing account is safe: deduplication makes the second run add only what is new.
    /// </para>
    ///
    /// <para>
    /// <see cref="BankStatementImportRequest.SelectedAccountId"/> is applied only when exactly
    /// one statement in the file is unmatched. One dropdown cannot express an N-to-N mapping,
    /// and guessing which of several unmatched IBANs the user meant would post transactions to
    /// the wrong account.
    /// </para>
    /// </summary>
    public IReadOnlyList<BankStatementImportResult> ImportCamt053(
        BankStatementImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FileBytes);
        ArgumentNullException.ThrowIfNull(request.Accounts);
        ArgumentNullException.ThrowIfNull(request.ExistingImportedTransactions);

        if (request.FileBytes.Length == 0)
        {
            throw new InvalidOperationException("The imported CAMT.053 file is empty.");
        }

        var asOfDate = request.AsOfDate
            ?? DateOnly.FromDateTime(DateTime.Today);

        var fileFingerprint = ImportedBankFileFingerprint.Create(
            request.FileBytes);

        var file = _camt053Parser.Parse(request.FileBytes);

        var selectedAccount = ResolveSelectedAccount(
            request.Accounts,
            request.SelectedAccountId);

        var matches = file.Statements
            .Select(statement => new
            {
                Statement = statement,
                Match = ResolveCamt053Account(request.Accounts, statement)
            })
            .ToList();

        var unmatchedCount = matches.Count(x => x.Match.Account is null);

        var results = new List<BankStatementImportResult>(matches.Count);

        // Merging is chained: statement N sees what statement N-1 added. That matters when one
        // file contains two statements for the same account (paginated exports do this) - the
        // second must not re-add what the first already contributed.
        var existingTransactions = request.ExistingImportedTransactions;

        foreach (var item in matches)
        {
            var statement = item.Statement;

            var match = item.Match.Account is null && selectedAccount is not null && unmatchedCount == 1
                ? new AccountMatch(selectedAccount, BankStatementAccountMatchStatus.SelectedManually)
                : item.Match;

            var preview = CreateCamt053Preview(
                statement,
                request.FileName,
                fileFingerprint);

            if (match.Account is null)
            {
                results.Add(new BankStatementImportResult
                {
                    Preview = preview,
                    AccountMatchStatus = BankStatementAccountMatchStatus.NotMatched,
                    AccountId = null,
                    MappingResult = null,
                    MergeResult = null,
                    SuggestedBalanceUpdate = null,
                    BankAccountIdentifierToRemember = statement.AccountIdentifier
                });

                continue;
            }

            var mappingResult = _mapper.MapFromCamt053(
                statement,
                match.Account.Id,
                fileFingerprint,
                request.FileName);

            var mergeResult = _merger.Merge(
                existingTransactions,
                mappingResult);

            existingTransactions = mergeResult.MergedTransactions;

            results.Add(new BankStatementImportResult
            {
                Preview = preview,
                AccountMatchStatus = match.Status,
                AccountId = match.Account.Id,
                MappingResult = mappingResult,
                MergeResult = mergeResult,
                SuggestedBalanceUpdate = CreateSuggestedBalanceUpdate(
                    match.Account.Id,
                    mappingResult.Batch,
                    asOfDate),
                BankAccountIdentifierToRemember =
                    match.Status == BankStatementAccountMatchStatus.SelectedManually
                        ? statement.AccountIdentifier
                        : null
            });
        }

        return results;
    }

    private static BankStatementImportPreview CreateCamt053Preview(
        Camt053Statement statement,
        string? fileName,
        string fileFingerprint)
    {
        var firstEntryDate = statement.Entries.Count == 0
            ? (DateOnly?)null
            : statement.Entries.Min(x => x.ValueDate);

        var lastEntryDate = statement.Entries.Count == 0
            ? (DateOnly?)null
            : statement.Entries.Max(x => x.ValueDate);

        return new BankStatementImportPreview
        {
            SourceFormat = Camt053SourceFormat,
            FileName = fileName,
            FileFingerprint = fileFingerprint,
            BankAccountIdentifier = statement.AccountIdentifier,
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
            FirstTransactionDate = firstEntryDate,
            LastTransactionDate = lastEntryDate,
            ParsedTransactionCount = statement.Entries.Count,
            // Entry level only. Adding TxDtls amounts here would double-count every internal
            // batch booking.
            TransactionNetAmount = statement.EntryNetAmount,
            ReconciliationAvailable = statement.Reconciliation.IsAvailable,
            ReconciliationBalanced = statement.Reconciliation.IsBalanced,
            ReconciliationDifference = statement.Reconciliation.Difference
        };
    }

    /// <summary>
    /// Matches a statement to an account by IBAN.
    ///
    /// Three sources, in order of confidence: a stored <see cref="AccountBankIdentifierType.Iban"/>
    /// identifier, the account's own <c>Iban</c> field (so an account the user filled in by hand
    /// matches without any import-specific setup), and finally a stored MT940 account id, which
    /// on Swiss statements is IBAN-shaped and lets an account already used for MT940 keep
    /// matching after the bank switches format.
    /// </summary>
    private static AccountMatch ResolveCamt053Account(
        IReadOnlyCollection<Account> accounts,
        Camt053Statement statement)
    {
        var accountIdentifier = statement.AccountIdentifier;

        if (string.IsNullOrWhiteSpace(accountIdentifier))
        {
            return new AccountMatch(null, BankStatementAccountMatchStatus.NotMatched);
        }

        var normalizedIdentifier = AccountBankIdentifier.Normalize(accountIdentifier);

        var matched =
            FindSingle(
                accounts,
                account => AccountBankIdentifierMatcher.HasIdentifier(
                    account,
                    AccountBankIdentifierType.Iban,
                    accountIdentifier))
            ?? FindSingle(
                accounts,
                account => account.Iban is not null &&
                    string.Equals(
                        AccountBankIdentifier.Normalize(account.Iban),
                        normalizedIdentifier,
                        StringComparison.OrdinalIgnoreCase))
            ?? FindSingle(
                accounts,
                account => AccountBankIdentifierMatcher.HasMt940AccountId(
                    account,
                    accountIdentifier));

        return matched is null
            ? new AccountMatch(null, BankStatementAccountMatchStatus.NotMatched)
            : new AccountMatch(matched, BankStatementAccountMatchStatus.MatchedByBankIdentifier);
    }

    /// <summary>
    /// Returns the matching account, or <c>null</c> when none matches <b>or several do</b>.
    ///
    /// An ambiguous match is treated as no match on purpose: the user is then asked which
    /// account to use, instead of the import throwing or picking one at random.
    /// </summary>
    private static Account? FindSingle(
        IReadOnlyCollection<Account> accounts,
        Func<Account, bool> predicate)
    {
        Account? found = null;

        foreach (var account in accounts)
        {
            if (!predicate(account))
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = account;
        }

        return found;
    }

    private static Account? ResolveSelectedAccount(
        IReadOnlyCollection<Account> accounts,
        Guid? selectedAccountId)
    {
        if (selectedAccountId is null)
        {
            return null;
        }

        return accounts.SingleOrDefault(x => x.Id == selectedAccountId.Value)
            ?? throw new InvalidOperationException(
                $"Selected account '{selectedAccountId}' does not exist.");
    }

    public BankStatementImportResult ImportMt940(
        BankStatementImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FileBytes);
        ArgumentNullException.ThrowIfNull(request.Accounts);
        ArgumentNullException.ThrowIfNull(request.ExistingImportedTransactions);

        if (request.FileBytes.Length == 0)
        {
            throw new InvalidOperationException("The imported MT940 file is empty.");
        }

        var asOfDate = request.AsOfDate
            ?? DateOnly.FromDateTime(DateTime.Today);

        var fileFingerprint = ImportedBankFileFingerprint.Create(
            request.FileBytes);

        var statement = _mt940Parser.Parse(
            request.FileBytes);

        var preview = CreatePreview(
            statement,
            request.FileName,
            fileFingerprint);

        var accountMatch = ResolveAccount(
            request.Accounts,
            statement.AccountIdentifier,
            request.SelectedAccountId);

        if (accountMatch.Account is null)
        {
            return new BankStatementImportResult
            {
                Preview = preview,
                AccountMatchStatus = BankStatementAccountMatchStatus.NotMatched,
                AccountId = null,
                MappingResult = null,
                MergeResult = null,
                SuggestedBalanceUpdate = null,
                BankAccountIdentifierToRemember = statement.AccountIdentifier
            };
        }

        var mappingResult = _mapper.MapFromMt940(
            statement,
            accountMatch.Account.Id,
            fileFingerprint,
            request.FileName);

        var mergeResult = _merger.Merge(
            request.ExistingImportedTransactions,
            mappingResult);

        var suggestedBalanceUpdate = CreateSuggestedBalanceUpdate(
            accountMatch.Account.Id,
            mappingResult.Batch,
            asOfDate);

        return new BankStatementImportResult
        {
            Preview = preview,
            AccountMatchStatus = accountMatch.Status,
            AccountId = accountMatch.Account.Id,
            MappingResult = mappingResult,
            MergeResult = mergeResult,
            SuggestedBalanceUpdate = suggestedBalanceUpdate,
            BankAccountIdentifierToRemember = accountMatch.Status == BankStatementAccountMatchStatus.SelectedManually
                ? statement.AccountIdentifier
                : null
        };
    }

    private static BankStatementImportPreview CreatePreview(
        Mt940Statement statement,
        string? fileName,
        string fileFingerprint)
    {
        var firstTransactionDate = statement.Transactions.Count == 0
            ? (DateOnly?)null
            : statement.Transactions.Min(x => x.ValueDate);

        var lastTransactionDate = statement.Transactions.Count == 0
            ? (DateOnly?)null
            : statement.Transactions.Max(x => x.ValueDate);

        return new BankStatementImportPreview
        {
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
            FirstTransactionDate = firstTransactionDate,
            LastTransactionDate = lastTransactionDate,
            ParsedTransactionCount = statement.Transactions.Count,
            TransactionNetAmount = statement.Transactions.Sum(x => x.SignedAmount),
            ReconciliationAvailable = statement.Reconciliation.IsAvailable,
            ReconciliationBalanced = statement.Reconciliation.IsBalanced,
            ReconciliationDifference = statement.Reconciliation.Difference
        };
    }

    private static AccountMatch ResolveAccount(
        IReadOnlyCollection<Account> accounts,
        string? statementAccountIdentifier,
        Guid? selectedAccountId)
    {
        if (!string.IsNullOrWhiteSpace(statementAccountIdentifier))
        {
            var matchedByIdentifier = AccountBankIdentifierMatcher.FindByMt940AccountId(
                accounts,
                statementAccountIdentifier);

            if (matchedByIdentifier is not null)
            {
                return new AccountMatch(
                    matchedByIdentifier,
                    BankStatementAccountMatchStatus.MatchedByBankIdentifier);
            }
        }

        if (selectedAccountId is null)
        {
            return new AccountMatch(
                null,
                BankStatementAccountMatchStatus.NotMatched);
        }

        var selectedAccount = accounts.SingleOrDefault(x => x.Id == selectedAccountId.Value);

        if (selectedAccount is null)
        {
            throw new InvalidOperationException(
                $"Selected account '{selectedAccountId}' does not exist.");
        }

        return new AccountMatch(
            selectedAccount,
            BankStatementAccountMatchStatus.SelectedManually);
    }

    private static BankStatementSuggestedBalanceUpdate? CreateSuggestedBalanceUpdate(
        Guid accountId,
        ImportedBankStatementBatch batch,
        DateOnly asOfDate)
    {
        if (batch.ClosingBalance is null)
        {
            return null;
        }

        var closingBalanceDateFromFile = batch.ClosingBalanceDate;
        var lastTransactionDate = batch.LastTransactionDate;

        var suggestedBalanceDate = DetermineSuggestedBalanceDate(
            closingBalanceDateFromFile,
            lastTransactionDate,
            asOfDate);

        var closingBalanceDateLooksSuspicious =
            closingBalanceDateFromFile is not null &&
            lastTransactionDate is not null &&
            closingBalanceDateFromFile.Value > lastTransactionDate.Value;

        var reason = closingBalanceDateLooksSuspicious
            ? "The statement closing balance date is after the latest transaction date. The latest transaction date was used as suggested balance date."
            : "The statement closing balance date was used as suggested balance date.";

        return new BankStatementSuggestedBalanceUpdate
        {
            AccountId = accountId,
            Balance = batch.ClosingBalance.Value,
            Currency = batch.Currency,
            BalanceDate = suggestedBalanceDate,
            ClosingBalanceDateFromFile = closingBalanceDateFromFile,
            LastTransactionDate = lastTransactionDate,
            ClosingBalanceDateLooksSuspicious = closingBalanceDateLooksSuspicious,
            Reason = reason
        };
    }

    private static DateOnly DetermineSuggestedBalanceDate(
        DateOnly? closingBalanceDateFromFile,
        DateOnly? lastTransactionDate,
        DateOnly asOfDate)
    {
        if (closingBalanceDateFromFile is null && lastTransactionDate is null)
        {
            return asOfDate;
        }

        if (closingBalanceDateFromFile is null)
        {
            return lastTransactionDate!.Value;
        }

        if (lastTransactionDate is null)
        {
            return closingBalanceDateFromFile.Value;
        }

        if (closingBalanceDateFromFile.Value > lastTransactionDate.Value)
        {
            return lastTransactionDate.Value;
        }

        return closingBalanceDateFromFile.Value;
    }

    private sealed record AccountMatch(
        Account? Account,
        BankStatementAccountMatchStatus Status);
}