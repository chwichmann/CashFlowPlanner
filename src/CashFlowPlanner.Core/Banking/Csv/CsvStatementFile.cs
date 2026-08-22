namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// A parsed CSV statement: the rows, and - just as importantly - every decision the parser had
/// to make to produce them.
///
/// <para>
/// The decisions are first-class here rather than internal, because CSV has no self-describing
/// header the way camt.053 does. The only way a user can tell a correct import from a plausible
/// wrong one is to be shown which column was read as the amount, which character was taken as
/// the decimal point and how the dates were read - before anything is committed.
/// </para>
/// </summary>
public sealed class CsvStatementFile
{
    public required string ProfileId { get; init; }

    public required string ProfileDisplayName { get; init; }

    /// <summary>True when the profile inferred the lexical settings rather than stating them.</summary>
    public bool WasAutoDetected { get; init; }

    public required char Delimiter { get; init; }

    public required CsvDecimalSeparator DecimalSeparator { get; init; }

    /// <summary>The date format actually used, for display. Null only when the file has no rows.</summary>
    public string? DateFormat { get; init; }

    public required CsvTextEncoding Encoding { get; init; }

    /// <summary>1-based physical line the header was found on. Everything above it is preamble.</summary>
    public required int HeaderLineNumber { get; init; }

    /// <summary>The rows above the header, kept for display and scanned for an IBAN.</summary>
    public IReadOnlyList<string> PreambleLines { get; init; } = [];

    public required CsvColumnMapping Mapping { get; init; }

    public required CsvAmountConvention AmountConvention { get; init; }

    public IReadOnlyList<CsvStatementRow> Rows { get; init; } = [];

    public IReadOnlyList<CsvRowIssue> Issues { get; init; } = [];

    public IReadOnlyList<CsvParseWarning> Warnings { get; init; } = [];

    /// <summary>
    /// An IBAN found in the preamble, if any. CSV exports have no equivalent of camt's
    /// <c>Acct/Id/IBAN</c>, so this is best-effort - and when it comes up empty the user picks
    /// the account by hand, which is the same path an unmatched camt statement takes.
    /// </summary>
    public string? AccountIdentifier { get; init; }

    public string Currency { get; init; } = "CHF";

    public required CsvReconciliationResult Reconciliation { get; init; }

    public decimal TransactionNetAmount =>
        Rows.Sum(x => x.SignedAmount);

    public DateOnly? FirstTransactionDate =>
        Rows.Count == 0 ? null : Rows.Min(x => x.EffectiveDate);

    public DateOnly? LastTransactionDate =>
        Rows.Count == 0 ? null : Rows.Max(x => x.EffectiveDate);
}
