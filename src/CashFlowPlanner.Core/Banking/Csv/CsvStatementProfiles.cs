namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// The built-in profiles.
///
/// <para>
/// <b>Named after shapes, not after banks.</b> That is a deliberate refusal, not an oversight.
/// Hardcoding "PostFinance" onto a column layout nobody here has verified against a real
/// PostFinance export produces the worst possible failure: a user picks the profile with their
/// bank's name on it, trusts it because it is named after their bank, and imports a column of
/// balances as amounts. A profile called "Semicolon, Swiss numbers" makes no promise it cannot
/// keep, and the preview shows exactly what it did.
/// </para>
///
/// <para>
/// What these profiles pin down is the <i>lexical</i> layer - delimiter, decimal separator,
/// date format, encoding - which is where a wrong guess costs money and where the shapes really
/// are only a handful. The <i>column</i> layer stays alias-driven in every profile, because
/// header spellings vary far more than number formats do and matching them is something the
/// resolver does better than an author with one sample file.
/// </para>
///
/// <para>
/// Adding a genuine bank profile is adding one entry to <see cref="All"/> with that bank's real
/// export in the test fixtures beside it. Until such a fixture exists, the honest offer is
/// <see cref="Auto"/> plus a preview the user confirms.
/// </para>
/// </summary>
public static class CsvStatementProfiles
{
    public const string AutoId = "auto";
    public const string SwissSemicolonId = "swiss-semicolon";
    public const string SwissCommaId = "swiss-comma";
    public const string GermanSemicolonId = "german-semicolon";
    public const string IsoCommaId = "iso-comma";
    public const string DebitCreditSemicolonId = "debit-credit-semicolon";
    public const string TabSeparatedId = "tab-separated";

    /// <summary>
    /// Infers everything from the file. The default, and the one to reach for first: it tests
    /// its guesses against every row rather than asserting them, so it is right more often than
    /// a profile chosen from a dropdown by someone who has not looked inside the file.
    /// </summary>
    public static readonly CsvStatementProfile Auto = new()
    {
        Id = AutoId,
        DisplayName = "Automatic detection",
        IsAutoDetect = true
    };

    /// <summary>
    /// Semicolon-separated with Swiss numbers: <c>1'234.56</c>, <c>15.01.2026</c>. The most
    /// common shape a Swiss bank's German-language web banking produces.
    /// </summary>
    public static readonly CsvStatementProfile SwissSemicolon = new()
    {
        Id = SwissSemicolonId,
        DisplayName = "Semicolon, Swiss numbers (1'234.56), dd.MM.yyyy",
        Delimiter = ';',
        DecimalSeparator = CsvDecimalSeparator.Dot,
        DateFormats = ["d.M.yyyy", "d.M.yy"]
    };

    /// <summary>Comma-separated with Swiss numbers. Grouping apostrophes make the comma safe as a delimiter.</summary>
    public static readonly CsvStatementProfile SwissComma = new()
    {
        Id = SwissCommaId,
        DisplayName = "Comma, Swiss numbers (1'234.56), dd.MM.yyyy",
        Delimiter = ',',
        DecimalSeparator = CsvDecimalSeparator.Dot,
        DateFormats = ["d.M.yyyy", "d.M.yy"]
    };

    /// <summary>
    /// Semicolon-separated with German numbers: <c>1.234,56</c>. The semicolon is not optional
    /// here - a comma decimal separator and a comma delimiter cannot coexist.
    /// </summary>
    public static readonly CsvStatementProfile GermanSemicolon = new()
    {
        Id = GermanSemicolonId,
        DisplayName = "Semicolon, German numbers (1.234,56), dd.MM.yyyy",
        Delimiter = ';',
        DecimalSeparator = CsvDecimalSeparator.Comma,
        DateFormats = ["d.M.yyyy", "d.M.yy"]
    };

    /// <summary>
    /// Comma-separated, ISO dates, plain <c>1234.56</c> amounts. What the neobanks and the
    /// English-language exports produce, and what a spreadsheet saves as "CSV UTF-8".
    /// </summary>
    public static readonly CsvStatementProfile IsoComma = new()
    {
        Id = IsoCommaId,
        DisplayName = "Comma, 1234.56, yyyy-MM-dd",
        Delimiter = ',',
        DecimalSeparator = CsvDecimalSeparator.Dot,
        DateFormats = ["yyyy-M-d"],
        Encoding = CsvTextEncoding.Utf8
    };

    /// <summary>
    /// Semicolon-separated with separate Belastung and Gutschrift columns instead of one signed
    /// amount. Stating the convention matters: with only one of the two columns filled per row,
    /// inference can be fooled by a file whose first hundred rows are all expenses.
    /// </summary>
    public static readonly CsvStatementProfile DebitCreditSemicolon = new()
    {
        Id = DebitCreditSemicolonId,
        DisplayName = "Semicolon, separate debit and credit columns",
        Delimiter = ';',
        DecimalSeparator = CsvDecimalSeparator.Dot,
        DateFormats = ["d.M.yyyy", "d.M.yy"],
        AmountConvention = CsvAmountConvention.SeparateDebitCredit
    };

    /// <summary>Tab-separated. Rare from banks, common from a spreadsheet the user re-saved.</summary>
    public static readonly CsvStatementProfile TabSeparated = new()
    {
        Id = TabSeparatedId,
        DisplayName = "Tab-separated",
        Delimiter = '\t'
    };

    public static readonly IReadOnlyList<CsvStatementProfile> All =
    [
        Auto,
        SwissSemicolon,
        SwissComma,
        GermanSemicolon,
        DebitCreditSemicolon,
        IsoComma,
        TabSeparated
    ];

    /// <summary>
    /// Looks a profile up by id, falling back to <see cref="Auto"/>.
    ///
    /// Falls back rather than throwing because the id arrives from persisted state and from a
    /// query string: a stale id should re-detect, not break the import screen.
    /// </summary>
    public static CsvStatementProfile Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Auto;
        }

        return All.FirstOrDefault(
            profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Auto;
    }
}
