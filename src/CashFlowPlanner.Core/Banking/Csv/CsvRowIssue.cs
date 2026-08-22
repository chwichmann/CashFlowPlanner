namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// One row that could not be imported, kept so the preview can list it.
///
/// <para>
/// Dropping unreadable rows silently is the failure mode that matters here: an import that
/// says "312 transactions added" while three rows fell out is an import the user will trust and
/// a plan that is quietly short by three transactions. Every one of these is rendered, with the
/// physical line number and the raw text, so the user can go and look.
/// </para>
/// </summary>
public sealed class CsvRowIssue
{
    public required int LineNumber { get; init; }

    public required CsvRowIssueKind Kind { get; init; }

    /// <summary>The row exactly as it appeared in the file.</summary>
    public required string RawText { get; init; }

    /// <summary>The header of the offending column, when the problem is about one column.</summary>
    public string? ColumnHeader { get; init; }

    /// <summary>The cell value that could not be read.</summary>
    public string? Value { get; init; }
}
