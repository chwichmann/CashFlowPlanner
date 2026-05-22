using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Banking.Import;

public sealed class BankStatementImportRequest
{
    public required byte[] FileBytes { get; init; }

    public string? FileName { get; init; }

    public required IReadOnlyCollection<Account> Accounts { get; init; }

    public IReadOnlyCollection<ImportedBankTransaction> ExistingImportedTransactions { get; init; } = [];

    /// <summary>
    /// Optional account selected by the user when automatic matching by bank identifier is not possible.
    /// </summary>
    public Guid? SelectedAccountId { get; init; }

    /// <summary>
    /// Date used for preview logic. Defaults to today's local date when omitted.
    /// </summary>
    public DateOnly? AsOfDate { get; init; }
}
