namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Why one row could not be turned into a transaction.
///
/// <para>
/// A closed set rather than free text, so the UI can translate it. A user reading
/// "Zeile 47: Betrag nicht lesbar" can go and look; a user reading an English exception message
/// from a parser cannot.
/// </para>
/// </summary>
public enum CsvRowIssueKind
{
    /// <summary>The row has fewer fields than the header, so the mapped columns are not all there.</summary>
    TooFewColumns = 0,

    /// <summary>The date cell was empty.</summary>
    MissingDate = 1,

    /// <summary>The date cell did not match the file's date format.</summary>
    UnreadableDate = 2,

    /// <summary>No amount at all: the amount cell was empty, or both debit and credit were.</summary>
    MissingAmount = 3,

    /// <summary>There was an amount, but it did not read as a number under the chosen decimal separator.</summary>
    UnreadableAmount = 4,

    /// <summary>Debit and credit were both filled in. The row is genuinely contradictory and is not guessed at.</summary>
    BothDebitAndCredit = 5,

    /// <summary>An amount plus a direction column whose value was neither a debit nor a credit marker.</summary>
    UnreadableDebitCreditIndicator = 6,

    /// <summary>A balance cell that did not read as a number. The transaction still imports; only reconciliation is lost.</summary>
    UnreadableBalance = 7
}
