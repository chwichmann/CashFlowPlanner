namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// A CSV import profile: <b>data, not code</b>.
///
/// <para>
/// There is no Swiss CSV standard. Banana Accounting ships a separately authored import
/// extension per bank, and PostFinance alone emits several mutually incompatible shapes. A
/// class per bank would mean a code change, a build and a release every time one of thirty
/// banks renames a column - and thirty near-identical classes whose differences are four
/// literals each.
/// </para>
///
/// <para>
/// So a profile is a record of literals. Everything that varies between banks is a property
/// here: the delimiter, the quote character, which character is the decimal point, the date
/// formats, the encoding, how many rows of preamble to skip, and which header spellings map to
/// which field. Adding a bank is adding an entry to
/// <see cref="CsvStatementProfiles"/>; it never touches the parser.
/// </para>
///
/// <para>
/// Every property has a defensible default, and the defaults together <i>are</i> the
/// auto-detecting profile. That is the point: a profile does not have to describe a bank
/// completely, it only has to pin down the parts the file cannot be trusted to reveal. A
/// profile that pins the decimal separator and lets everything else be inferred is a perfectly
/// good profile, and is safer than one that guesses at column indexes it has never seen.
/// </para>
/// </summary>
public sealed class CsvStatementProfile
{
    /// <summary>Stable identifier, persisted in the batch and passed back from the UI.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// English name, used as the fallback when the UI has no translation for
    /// <see cref="Id"/>. Profiles are named after the <i>shape</i> they describe, never after a
    /// bank whose export has not been seen - see <see cref="CsvStatementProfiles"/>.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>True for the profile that infers everything. Exactly one built-in profile sets it.</summary>
    public bool IsAutoDetect { get; init; }

    /// <summary>Field separator, or <c>null</c> to infer it from the header row.</summary>
    public char? Delimiter { get; init; }

    /// <summary>RFC 4180 quote character. No Swiss export has been seen using anything else.</summary>
    public char Quote { get; init; } = '"';

    public CsvDecimalSeparator DecimalSeparator { get; init; } = CsvDecimalSeparator.Auto;

    /// <summary>
    /// Accepted date formats, most preferred first. Empty means infer, which is the safer
    /// default: inference tests every candidate against every date in the file, while a stated
    /// format that turns out to be wrong makes every row unreadable.
    /// </summary>
    public IReadOnlyList<string> DateFormats { get; init; } = [];

    public CsvTextEncoding Encoding { get; init; } = CsvTextEncoding.Auto;

    /// <summary>
    /// Rows to drop before the header, or <c>null</c> to search for the header instead.
    ///
    /// <para>
    /// Searching is the default and handles the PostFinance-style preamble - account number,
    /// date range, currency, a blank line - without anybody having to count the rows, and keeps
    /// working when the bank adds a line. A fixed count exists for the files where the preamble
    /// itself contains something that looks like a header row.
    /// </para>
    /// </summary>
    public int? PreambleRowsToSkip { get; init; }

    /// <summary>How far into the file to look for the header before giving up.</summary>
    public int HeaderSearchRowLimit { get; init; } = 40;

    /// <summary>
    /// False for exports with no header at all, which then need
    /// <see cref="ColumnIndexOverrides"/> to say what the columns are.
    /// </summary>
    public bool HasHeaderRow { get; init; } = true;

    public CsvAmountConvention AmountConvention { get; init; } = CsvAmountConvention.Auto;

    /// <summary>
    /// Whether a debit column holds the amount unsigned, so it has to be negated.
    ///
    /// <para>
    /// Almost always true - a column headed <c>Belastung</c> holding <c>-45.60</c> would be a
    /// double negative. But some exports do sign it, and negating a value that is already
    /// negative turns an expense into income, which the plan then treats as salary. When
    /// <see cref="CsvAmountConvention.SeparateDebitCredit"/> is inferred rather than stated, the
    /// parser checks the actual values instead of trusting this flag.
    /// </para>
    /// </summary>
    public bool DebitColumnIsUnsigned { get; init; } = true;

    /// <summary>Values of a direction column that mean money out.</summary>
    public IReadOnlyList<string> DebitIndicators { get; init; } =
        ["s", "d", "soll", "debit", "debet", "dr", "-", "belastung", "out", "sortie", "dare"];

    /// <summary>Values of a direction column that mean money in.</summary>
    public IReadOnlyList<string> CreditIndicators { get; init; } =
        ["h", "c", "haben", "credit", "cr", "+", "gutschrift", "in", "entree", "avere"];

    /// <summary>Currency assumed when the file states none per row and none in the preamble.</summary>
    public string DefaultCurrency { get; init; } = "CHF";

    /// <summary>
    /// Extra header spellings for this bank, merged <i>in front of</i>
    /// <see cref="CsvColumnAliases.Default"/> rather than replacing it.
    /// </summary>
    public IReadOnlyDictionary<CsvColumnRole, IReadOnlyList<string>> ColumnHeaderOverrides
    { get; init; } = new Dictionary<CsvColumnRole, IReadOnlyList<string>>();

    /// <summary>
    /// Fixed column indexes, 0-based. Wins over anything inferred from the header. Only for
    /// headerless exports, or for a header so eccentric that no alias will ever match it.
    /// </summary>
    public IReadOnlyDictionary<CsvColumnRole, int> ColumnIndexOverrides { get; init; } =
        new Dictionary<CsvColumnRole, int>();
}
