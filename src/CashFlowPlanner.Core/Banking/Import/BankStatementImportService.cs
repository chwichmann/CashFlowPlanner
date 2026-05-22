using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Banking.Mt940;

namespace CashFlowPlanner.Core.Banking.Import;

public sealed class BankStatementImportService
{
    private readonly Mt940Parser _mt940Parser;
    private readonly ImportedBankStatementMapper _mapper;
    private readonly ImportedBankTransactionMerger _merger;

    public BankStatementImportService()
        : this(
            new Mt940Parser(),
            new ImportedBankStatementMapper(),
            new ImportedBankTransactionMerger())
    {
    }

    public BankStatementImportService(
        Mt940Parser mt940Parser,
        ImportedBankStatementMapper mapper,
        ImportedBankTransactionMerger merger)
    {
        _mt940Parser = mt940Parser;
        _mapper = mapper;
        _merger = merger;
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
            ? "The MT940 closing balance date is after the latest transaction date. The latest transaction date was used as suggested balance date."
            : "The MT940 closing balance date was used as suggested balance date.";

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