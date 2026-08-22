using System.Text;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Banking.Camt;
using CashFlowPlanner.Core.Banking.Csv;
using CashFlowPlanner.Core.Banking.Mt940;

namespace CashFlowPlanner.Core.Banking.Import;

public sealed class BankStatementImportService
{
    public const string Mt940SourceFormat = "MT940";

    public const string Camt053SourceFormat = "CAMT053";

    /// <summary>
    /// <b>The format that is always available to a private customer.</b> camt.053 is a
    /// business-banking feature at most Swiss retail banks - it has to be switched on through a
    /// business channel - and it does not exist at all at the neobanks. For several banks a CSV
    /// export is not the convenient path, it is the only one.
    /// </summary>
    public const string CsvSourceFormat = "CSV";

    private readonly Mt940Parser _mt940Parser;
    private readonly Camt053Parser _camt053Parser;
    private readonly CsvStatementParser _csvParser;
    private readonly ImportedBankStatementMapper _mapper;
    private readonly ImportedBankTransactionMerger _merger;

    public BankStatementImportService()
        : this(
            new Mt940Parser(),
            new Camt053Parser(),
            new CsvStatementParser(),
            new ImportedBankStatementMapper(),
            new ImportedBankTransactionMerger())
    {
    }

    public BankStatementImportService(
        Mt940Parser mt940Parser,
        Camt053Parser camt053Parser,
        ImportedBankStatementMapper mapper,
        ImportedBankTransactionMerger merger)
        : this(
            mt940Parser,
            camt053Parser,
            new CsvStatementParser(),
            mapper,
            merger)
    {
    }

    public BankStatementImportService(
        Mt940Parser mt940Parser,
        Camt053Parser camt053Parser,
        CsvStatementParser csvParser,
        ImportedBankStatementMapper mapper,
        ImportedBankTransactionMerger merger)
    {
        _mt940Parser = mt940Parser;
        _camt053Parser = camt053Parser;
        _csvParser = csvParser;
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

        return DetectSourceFormat(request.FileBytes) switch
        {
            Camt053SourceFormat => ImportCamt053(request),
            CsvSourceFormat => [ImportCsv(request)],
            _ => [ImportMt940(request)]
        };
    }

    /// <summary>
    /// Decides whether a file is CAMT.053, CSV or MT940 by looking at the content, not the
    /// extension.
    ///
    /// <para>
    /// Extensions are unreliable here: banks export camt as <c>.xml</c>, <c>.txt</c> or inside a
    /// <c>.zip</c>, CSV exports arrive as <c>.csv</c>, <c>.txt</c> and occasionally <c>.xls</c>,
    /// and users rename files. The content is what decides.
    /// </para>
    ///
    /// <para>
    /// The order is not arbitrary and MT940 keeps the last word. camt.053 is unambiguous - an
    /// XML document with a <c>BkToCstmrStmt</c> element is camt.053 and nothing else is. MT940
    /// is checked next by its tag markers, so a real MT940 statement can never be mistaken for
    /// CSV even though it is full of colons and commas. Only what is neither is offered to the
    /// CSV sniff, and anything the CSV sniff also rejects still goes to MT940 - which means an
    /// unrecognisable file produces exactly the MT940 error message it produced before CSV
    /// existed.
    /// </para>
    /// </summary>
    public static string DetectSourceFormat(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        var head = ReadHead(fileBytes);

        if (Camt053Parser.LooksLikeCamt053(head))
        {
            return Camt053SourceFormat;
        }

        return CsvStatementParser.LooksLikeCsv(head)
            ? CsvSourceFormat
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
        // Generous enough that a CSV header row still falls inside it after a long preamble.
        // camt sniffing is unaffected: LooksLikeCamt053 truncates to its own 4 KB regardless.
        const int HeadLength = 16384;

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

    /// <summary>
    /// Imports a CSV export. Always one result: a CSV file is one account's transactions, with
    /// no equivalent of camt's <c>Stmt</c> repetition.
    ///
    /// <para>
    /// Account matching is best-effort by design. A CSV export has no <c>Acct/Id/IBAN</c>, so
    /// the only chance of matching automatically is an IBAN in the preamble above the header -
    /// which is there often enough to be worth scanning for, and validated by its check digits
    /// so a QR reference cannot be mistaken for one. When nothing is found the result comes back
    /// with <see cref="BankStatementImportResult.RequiresAccountSelection"/> set and the user
    /// picks the account, exactly as an unmatched camt statement does. Once they do, the
    /// identifier is remembered on the account and the next month matches by itself.
    /// </para>
    ///
    /// <para>
    /// A file that cannot be parsed at all throws <see cref="CsvParseException"/> with a sentence
    /// the user can act on. Individual bad rows do not: they come back on
    /// <see cref="BankStatementImportCsvDetails.RowIssues"/> and are rendered, because an import
    /// that reports "312 added" while three rows fell out silently is worse than one that says
    /// which three.
    /// </para>
    /// </summary>
    public BankStatementImportResult ImportCsv(
        BankStatementImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FileBytes);
        ArgumentNullException.ThrowIfNull(request.Accounts);
        ArgumentNullException.ThrowIfNull(request.ExistingImportedTransactions);

        if (request.FileBytes.Length == 0)
        {
            throw new InvalidOperationException("The imported CSV file is empty.");
        }

        var asOfDate = request.AsOfDate
            ?? DateOnly.FromDateTime(DateTime.Today);

        var fileFingerprint = ImportedBankFileFingerprint.Create(
            request.FileBytes);

        var profile = CsvStatementProfiles.Find(request.CsvProfileId);

        var file = _csvParser.Parse(request.FileBytes, profile);

        var preview = CreateCsvPreview(
            file,
            request.FileName,
            fileFingerprint);

        var match = ResolveCsvAccount(
            request.Accounts,
            file.AccountIdentifier,
            request.SelectedAccountId);

        if (match.Account is null)
        {
            return new BankStatementImportResult
            {
                Preview = preview,
                AccountMatchStatus = BankStatementAccountMatchStatus.NotMatched,
                AccountId = null,
                MappingResult = null,
                MergeResult = null,
                SuggestedBalanceUpdate = null,
                BankAccountIdentifierToRemember = file.AccountIdentifier
            };
        }

        var mappingResult = _mapper.MapFromCsv(
            file,
            match.Account.Id,
            fileFingerprint,
            request.FileName);

        var mergeResult = _merger.Merge(
            request.ExistingImportedTransactions,
            mappingResult);

        return new BankStatementImportResult
        {
            Preview = preview,
            AccountMatchStatus = match.Status,
            AccountId = match.Account.Id,
            MappingResult = mappingResult,
            MergeResult = mergeResult,
            // Null whenever the export carried no balance column, which is the normal case.
            // CreateSuggestedBalanceUpdate returns null for a batch with no closing balance, so
            // a CSV import never proposes overwriting a balance the user maintains by hand.
            SuggestedBalanceUpdate = CreateSuggestedBalanceUpdate(
                match.Account.Id,
                mappingResult.Batch,
                asOfDate),
            BankAccountIdentifierToRemember =
                match.Status == BankStatementAccountMatchStatus.SelectedManually
                    ? file.AccountIdentifier
                    : null
        };
    }

    private static BankStatementImportPreview CreateCsvPreview(
        CsvStatementFile file,
        string? fileName,
        string fileFingerprint)
    {
        var reconciliation = file.Reconciliation;

        return new BankStatementImportPreview
        {
            SourceFormat = CsvSourceFormat,
            FileName = fileName,
            FileFingerprint = fileFingerprint,
            BankAccountIdentifier = file.AccountIdentifier,
            TransactionReference = null,
            StatementNumber = null,
            OpeningBalanceDate = reconciliation.IsAvailable ? reconciliation.OpeningBalanceDate : null,
            OpeningBalance = reconciliation.IsAvailable ? reconciliation.OpeningBalance : null,
            ClosingBalanceDate = reconciliation.IsAvailable ? reconciliation.ClosingBalanceDate : null,
            ClosingBalance = reconciliation.IsAvailable ? reconciliation.ClosingBalance : null,
            Currency = file.Currency,
            FirstTransactionDate = file.FirstTransactionDate,
            LastTransactionDate = file.LastTransactionDate,
            ParsedTransactionCount = file.Rows.Count,
            TransactionNetAmount = file.TransactionNetAmount,
            // False unless the file genuinely carried a running-balance column. Reporting
            // "balanced" because nothing contradicted us would turn the one check that catches a
            // half-downloaded statement into a green tick that means nothing.
            ReconciliationAvailable = reconciliation.IsAvailable,
            ReconciliationBalanced = reconciliation.IsBalanced,
            ReconciliationDifference = reconciliation.Difference,
            Csv = CreateCsvDetails(file)
        };
    }

    private static BankStatementImportCsvDetails CreateCsvDetails(CsvStatementFile file)
    {
        var columns = file.Mapping.ColumnIndexByRole
            .OrderBy(x => x.Value)
            .Select(x => new BankStatementImportCsvColumn(
                x.Key,
                x.Value,
                file.Mapping.HeaderOf(x.Key)))
            .ToList();

        var mappedIndexes = file.Mapping.ColumnIndexByRole.Values.ToHashSet();

        var unmappedHeaders = file.Mapping.Headers
            .Select((header, index) => (header, index))
            .Where(x => !mappedIndexes.Contains(x.index) && !string.IsNullOrWhiteSpace(x.header))
            .Select(x => x.header.Trim())
            .ToList();

        return new BankStatementImportCsvDetails
        {
            ProfileId = file.ProfileId,
            ProfileDisplayName = file.ProfileDisplayName,
            WasAutoDetected = file.WasAutoDetected,
            Delimiter = CsvStatementParser.DescribeDelimiter(file.Delimiter),
            DecimalSeparator = file.DecimalSeparator,
            DateFormat = file.DateFormat,
            Encoding = file.Encoding,
            HeaderLineNumber = file.HeaderLineNumber,
            PreambleLineCount = file.PreambleLines.Count,
            AmountConvention = file.AmountConvention,
            Columns = columns,
            UnmappedHeaders = unmappedHeaders,
            RowIssues = file.Issues,
            Warnings = file.Warnings
        };
    }

    /// <summary>
    /// Matches a CSV export to an account by the IBAN found in its preamble, falling back to the
    /// account the user picked. Same three lookups the camt path uses, for the same reason: an
    /// account whose IBAN the user typed into the account form must match without any
    /// import-specific setup.
    /// </summary>
    private static AccountMatch ResolveCsvAccount(
        IReadOnlyCollection<Account> accounts,
        string? accountIdentifier,
        Guid? selectedAccountId)
    {
        var matched = FindAccountByIban(accounts, accountIdentifier);

        if (matched is not null)
        {
            return new AccountMatch(
                matched,
                BankStatementAccountMatchStatus.MatchedByBankIdentifier);
        }

        if (selectedAccountId is null)
        {
            return new AccountMatch(null, BankStatementAccountMatchStatus.NotMatched);
        }

        return new AccountMatch(
            ResolveSelectedAccount(accounts, selectedAccountId),
            BankStatementAccountMatchStatus.SelectedManually);
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
        var matched = FindAccountByIban(accounts, statement.AccountIdentifier);

        return matched is null
            ? new AccountMatch(null, BankStatementAccountMatchStatus.NotMatched)
            : new AccountMatch(matched, BankStatementAccountMatchStatus.MatchedByBankIdentifier);
    }

    /// <summary>
    /// The three IBAN lookups, shared by the camt.053 and CSV paths so the two cannot drift.
    /// Returns null when none matches or several do - see <see cref="FindSingle"/>.
    /// </summary>
    private static Account? FindAccountByIban(
        IReadOnlyCollection<Account> accounts,
        string? accountIdentifier)
    {
        if (string.IsNullOrWhiteSpace(accountIdentifier))
        {
            return null;
        }

        var normalizedIdentifier = AccountBankIdentifier.Normalize(accountIdentifier);

        return FindSingle(
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