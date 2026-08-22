namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Thrown for CSV content the import cannot make sense of <b>as a whole</b>.
///
/// <para>
/// The bar is deliberately high. A single row with an unreadable amount is not a parse
/// failure - it is one row the user is shown and told about, while the other three hundred
/// still import. This type is reserved for the cases where there is nothing to import at
/// all: no delimiter, no header row, no date column, no amount column. Those the user can
/// actually act on, by picking a different profile or re-exporting.
/// </para>
///
/// <para>
/// Every failure path funnels through here, including the ones that would otherwise surface
/// as an <see cref="ArgumentException"/> from deep inside the reader, so the import UI shows
/// one actionable sentence rather than a stack trace.
/// </para>
/// </summary>
public sealed class CsvParseException : Exception
{
    public CsvParseException(string message)
        : base(message)
    {
    }

    public CsvParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
