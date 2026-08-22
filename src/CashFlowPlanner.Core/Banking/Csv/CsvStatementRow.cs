namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>One CSV row that did turn into a transaction.</summary>
public sealed class CsvStatementRow
{
    /// <summary>1-based physical line in the file. Shown in the preview so a row can be found again.</summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Position among the rows that parsed, 0-based.
    ///
    /// <para>
    /// Used for ordering and for reconciliation direction only - deliberately <b>not</b> part of
    /// any deduplication key. A bank that adds one row to next month's export would shift every
    /// index after it, and a key built on a shifting index re-imports the whole statement as new.
    /// </para>
    /// </summary>
    public required int RowIndex { get; init; }

    public DateOnly? BookingDate { get; init; }

    public DateOnly? ValueDate { get; init; }

    /// <summary>
    /// The date the plan books against: the value date when the file has one, otherwise the
    /// booking date. Same precedence MT940 and camt.053 use, so a household that switches
    /// format does not see its transactions move by a day.
    /// </summary>
    public DateOnly EffectiveDate =>
        ValueDate ?? BookingDate ?? default;

    public required decimal SignedAmount { get; init; }

    public string Currency { get; init; } = "CHF";

    public string? Description { get; init; }

    public string? Counterparty { get; init; }

    public string? Reference { get; init; }

    /// <summary>Running balance after this transaction, when the file has such a column.</summary>
    public decimal? Balance { get; init; }

    /// <summary>The row exactly as it appeared, for diagnostics and for the preview.</summary>
    public required string RawText { get; init; }
}
