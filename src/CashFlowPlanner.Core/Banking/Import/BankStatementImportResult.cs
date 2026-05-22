namespace CashFlowPlanner.Core.Banking.Import;

public sealed class BankStatementImportResult
{
    public required BankStatementImportPreview Preview { get; init; }

    public BankStatementAccountMatchStatus AccountMatchStatus { get; init; }

    public Guid? AccountId { get; init; }

    public bool RequiresAccountSelection =>
        AccountMatchStatus == BankStatementAccountMatchStatus.NotMatched ||
        AccountId is null;

    public ImportedBankStatementMappingResult? MappingResult { get; init; }

    public ImportedBankTransactionMergeResult? MergeResult { get; init; }

    public BankStatementSuggestedBalanceUpdate? SuggestedBalanceUpdate { get; init; }

    public bool CanImport =>
        AccountId is not null &&
        MappingResult is not null &&
        MergeResult is not null;

    public string? BankAccountIdentifierToRemember { get; init; }
}