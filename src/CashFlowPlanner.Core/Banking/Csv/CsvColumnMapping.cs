namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Which column index each role was found at, together with the header text it was found under.
///
/// <para>
/// This is the object the preview renders. A user must be able to read "Amount - column 3 -
/// <c>Betrag CHF</c>" before any of it lands in the plan, and that sentence is three lookups
/// into this type.
/// </para>
/// </summary>
public sealed class CsvColumnMapping
{
    public required IReadOnlyDictionary<CsvColumnRole, int> ColumnIndexByRole { get; init; }

    /// <summary>The header row as read, or synthesised column names when the profile has no header.</summary>
    public required IReadOnlyList<string> Headers { get; init; }

    public int? IndexOf(CsvColumnRole role)
    {
        return ColumnIndexByRole.TryGetValue(role, out var index)
            ? index
            : null;
    }

    public bool Has(CsvColumnRole role)
    {
        return ColumnIndexByRole.ContainsKey(role);
    }

    public string? HeaderOf(CsvColumnRole role)
    {
        var index = IndexOf(role);

        return index is null || index.Value >= Headers.Count
            ? null
            : Headers[index.Value];
    }

    /// <summary>
    /// A date column of some kind, and something that yields an amount. Below this the file is
    /// not a bank statement as far as this importer is concerned, and saying so is more useful
    /// than importing a column of zeroes.
    /// </summary>
    public bool HasMinimumViableMapping =>
        (Has(CsvColumnRole.BookingDate) || Has(CsvColumnRole.ValueDate))
        && (Has(CsvColumnRole.Amount) || Has(CsvColumnRole.Debit) || Has(CsvColumnRole.Credit));
}
