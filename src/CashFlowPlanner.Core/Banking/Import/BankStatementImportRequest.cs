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

    /// <summary>
    /// Which CSV profile to parse under. Ignored for MT940 and camt.053.
    ///
    /// <para>
    /// Null means <see cref="Csv.CsvStatementProfiles.Auto"/>, which is the right default:
    /// auto-detection tests its guesses against every row in the file, while a profile picked
    /// from a dropdown by someone who has not looked inside asserts things that may not hold.
    /// An unknown id also falls back to auto rather than failing - the id arrives from persisted
    /// UI state, and a stale one should re-detect rather than break the import screen.
    /// </para>
    /// </summary>
    public string? CsvProfileId { get; init; }
}
