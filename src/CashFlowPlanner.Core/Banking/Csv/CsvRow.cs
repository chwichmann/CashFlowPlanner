namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// One CSV record.
///
/// <para>
/// <see cref="LineNumber"/> is the 1-based <b>physical</b> line the record starts on, not its
/// index among the records. Those differ whenever a quoted field contains a newline - which
/// Swiss payment purposes routinely do - and the physical line is the one the user can find in
/// their editor when the preview tells them row 47 could not be read.
/// </para>
/// </summary>
public sealed class CsvRow
{
    public required int LineNumber { get; init; }

    public required IReadOnlyList<string> Fields { get; init; }

    /// <summary>The record exactly as it appeared, newlines and quoting included.</summary>
    public required string RawText { get; init; }

    public string Field(int index)
    {
        return index >= 0 && index < Fields.Count
            ? Fields[index]
            : string.Empty;
    }

    public bool IsEmpty =>
        Fields.All(string.IsNullOrWhiteSpace);
}
