using CashFlowPlanner.Core.Banking.Csv;

namespace CashFlowPlanner.Core.Banking.Import;

/// <summary>One column the CSV parser assigned a meaning to.</summary>
public sealed record BankStatementImportCsvColumn(
    CsvColumnRole Role,
    int ColumnIndex,
    string? Header)
{
    /// <summary>1-based, because that is how a person counts columns in a spreadsheet.</summary>
    public int ColumnNumber =>
        ColumnIndex + 1;
}

/// <summary>
/// Everything the CSV parser decided, carried on the preview so the user can check it before
/// committing.
///
/// <para>
/// This has no counterpart on the MT940 or camt.053 paths and should not have one. Those
/// formats are self-describing: a <c>:61:</c> record is an amount because the standard says so.
/// A CSV column is an amount because this importer guessed, and the only defence against a
/// wrong guess is showing it. "Amount - column 3 - Betrag CHF" has to be readable on screen
/// before a single franc lands in the plan.
/// </para>
/// </summary>
public sealed class BankStatementImportCsvDetails
{
    public required string ProfileId { get; init; }

    public required string ProfileDisplayName { get; init; }

    /// <summary>True when the settings below were inferred rather than stated by the profile.</summary>
    public bool WasAutoDetected { get; init; }

    /// <summary>The delimiter, already rendered for display - "Tab" rather than an invisible character.</summary>
    public required string Delimiter { get; init; }

    public required CsvDecimalSeparator DecimalSeparator { get; init; }

    public string? DateFormat { get; init; }

    public required CsvTextEncoding Encoding { get; init; }

    /// <summary>1-based physical line the header was found on. Everything above it was preamble.</summary>
    public int HeaderLineNumber { get; init; }

    public int PreambleLineCount { get; init; }

    public required CsvAmountConvention AmountConvention { get; init; }

    public IReadOnlyList<BankStatementImportCsvColumn> Columns { get; init; } = [];

    /// <summary>
    /// Headers no role was assigned to. Shown because an <i>unrecognised</i> column is how a
    /// user notices that their bank calls the amount something this importer has never heard of.
    /// </summary>
    public IReadOnlyList<string> UnmappedHeaders { get; init; } = [];

    public IReadOnlyList<CsvRowIssue> RowIssues { get; init; } = [];

    public IReadOnlyList<CsvParseWarning> Warnings { get; init; } = [];

    public int RowIssueCount =>
        RowIssues.Count;
}
