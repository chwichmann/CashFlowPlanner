using CashFlowPlanner.Core.Banking.Csv;

namespace CashFlowPlanner.Core.Tests.Banking.Csv;

/// <summary>
/// The parser measured against files written the way Swiss banks write them.
/// </summary>
public sealed class CsvStatementParserTests
{
    private static CsvStatementFile Parse(
        string fixtureName,
        CsvStatementProfile? profile = null)
    {
        return new CsvStatementParser().Parse(
            CsvFixture.ReadBytes(fixtureName),
            profile);
    }

    [Fact]
    public void Parse_ReadsSwissApostropheAmounts()
    {
        var file = Parse(CsvFixture.SwissApostrophe);

        Assert.Equal(';', file.Delimiter);
        Assert.Equal(CsvDecimalSeparator.Dot, file.DecimalSeparator);
        Assert.Equal("d.M.yyyy", file.DateFormat);
        Assert.Equal(CsvTextEncoding.Utf8, file.Encoding);

        Assert.Equal(
            [-2400.00m, -4.50m, -4.50m, 7350.55m, 128.40m, -12345.60m],
            file.Rows.Select(x => x.SignedAmount));

        Assert.Equal(new DateOnly(2026, 1, 15), file.Rows[0].EffectiveDate);
        Assert.Equal(new DateOnly(2026, 1, 31), file.Rows[5].EffectiveDate);
        Assert.All(file.Rows, row => Assert.Equal("CHF", row.Currency));
    }

    [Fact]
    public void Parse_MapsTheColumnsItFound()
    {
        var file = Parse(CsvFixture.SwissApostrophe);

        Assert.Equal(0, file.Mapping.IndexOf(CsvColumnRole.BookingDate));
        Assert.Equal(1, file.Mapping.IndexOf(CsvColumnRole.Description));
        Assert.Equal(2, file.Mapping.IndexOf(CsvColumnRole.Amount));
        Assert.Equal(3, file.Mapping.IndexOf(CsvColumnRole.Currency));

        Assert.Equal("Betrag", file.Mapping.HeaderOf(CsvColumnRole.Amount));
    }

    [Fact]
    public void Parse_KeepsADelimiterAndANewlineInsideAQuotedField()
    {
        var file = Parse(CsvFixture.SwissApostrophe);

        Assert.Equal("Rueckerstattung; Krankenkasse", file.Rows[4].Description);

        Assert.Contains("Zahlung", file.Rows[5].Description);
        Assert.Contains("ueber zwei Zeilen", file.Rows[5].Description);
    }

    [Fact]
    public void Parse_ReadsGermanNumberFormat()
    {
        var file = Parse(CsvFixture.GermanFormat);

        Assert.Equal(CsvDecimalSeparator.Comma, file.DecimalSeparator);

        Assert.Equal(
            [-2400.00m, -1234.56m, 7350.55m],
            file.Rows.Select(x => x.SignedAmount));
    }

    [Fact]
    public void Parse_PrefersTheValueDateOverTheBookingDate()
    {
        var file = Parse(CsvFixture.GermanFormat);

        Assert.Equal(new DateOnly(2026, 1, 16), file.Rows[1].BookingDate);
        Assert.Equal(new DateOnly(2026, 1, 17), file.Rows[1].ValueDate);
        Assert.Equal(new DateOnly(2026, 1, 17), file.Rows[1].EffectiveDate);
    }

    [Fact]
    public void Parse_ReadsTheCounterpartyAndTheDescriptionSeparately()
    {
        var file = Parse(CsvFixture.GermanFormat);

        Assert.Equal("Immobilien AG", file.Rows[0].Counterparty);
        Assert.Equal("Miete Januar", file.Rows[0].Description);
    }

    [Fact]
    public void Parse_TurnsSeparateDebitAndCreditColumnsIntoSignedAmounts()
    {
        var file = Parse(CsvFixture.DebitCreditColumns);

        Assert.Equal(CsvAmountConvention.SeparateDebitCredit, file.AmountConvention);

        Assert.Equal(
            [-2400.00m, 7350.55m, -45.60m],
            file.Rows.Select(x => x.SignedAmount));
    }

    /// <summary>
    /// The one case where a CSV import can reconcile: the export carried a running balance, so
    /// opening + net = closing is a real check rather than a green tick that means nothing.
    /// </summary>
    [Fact]
    public void Parse_ReconcilesAgainstARunningBalanceColumn()
    {
        var file = Parse(CsvFixture.DebitCreditColumns);

        Assert.True(file.Reconciliation.IsAvailable);
        Assert.True(file.Reconciliation.IsBalanced);
        Assert.Equal(15000.00m, file.Reconciliation.OpeningBalance);
        Assert.Equal(19904.95m, file.Reconciliation.ClosingBalance);
        Assert.Equal(0m, file.Reconciliation.Difference);
    }

    [Fact]
    public void Parse_ReportsNoReconciliationWhenThereIsNoBalanceColumn()
    {
        var file = Parse(CsvFixture.SwissApostrophe);

        Assert.False(file.Reconciliation.IsAvailable);
        Assert.False(file.Reconciliation.IsBalanced);
        Assert.Contains(CsvParseWarning.NoBalanceColumn, file.Warnings);
    }

    [Fact]
    public void Parse_SkipsPreambleRowsAndFindsTheHeaderBelowThem()
    {
        var file = Parse(CsvFixture.WithPreamble);

        Assert.Equal(6, file.HeaderLineNumber);
        Assert.Equal(4, file.PreambleLines.Count);
        Assert.Equal(2, file.Rows.Count);
        Assert.Equal(-2400.00m, file.Rows[0].SignedAmount);
    }

    [Fact]
    public void Parse_FindsTheAccountIbanInThePreamble()
    {
        var file = Parse(CsvFixture.WithPreamble);

        Assert.Equal("CH9300762011623852957", file.AccountIdentifier);
    }

    /// <summary>
    /// A lenient UTF-8 decode would not fail here - it would produce U+FFFD and the user would
    /// find "Z?rich Versicherung" in their transaction list with nothing to indicate a problem.
    /// </summary>
    [Fact]
    public void Parse_DecodesLatin1WithoutManglingUmlauts()
    {
        var file = Parse(CsvFixture.Latin1);

        Assert.Equal(CsvTextEncoding.Latin1, file.Encoding);
        Assert.Equal("Immobilien Bär AG", file.Rows[0].Counterparty);
        Assert.Equal("Miete Zürich", file.Rows[0].Description);
        Assert.Equal("Zürich Versicherung", file.Rows[1].Counterparty);
    }

    [Fact]
    public void Parse_ReadsAUtf8ByteOrderMarkWithoutLettingItLeakIntoTheFirstHeader()
    {
        var file = Parse(CsvFixture.Utf8Bom);

        Assert.Equal(0, file.Mapping.IndexOf(CsvColumnRole.BookingDate));
        Assert.Single(file.Rows);
    }

    [Fact]
    public void Parse_ReadsCommaDelimitedIsoFiles()
    {
        var file = Parse(CsvFixture.IsoComma);

        Assert.Equal(',', file.Delimiter);
        Assert.Equal("yyyy-M-d", file.DateFormat);

        Assert.Equal(
            [-2400.00m, 7350.55m],
            file.Rows.Select(x => x.SignedAmount));

        Assert.Equal(new DateOnly(2026, 1, 26), file.Rows[1].EffectiveDate);
    }

    [Fact]
    public void Parse_ReadsTabDelimitedFiles()
    {
        var file = Parse(CsvFixture.TabSeparated);

        Assert.Equal('\t', file.Delimiter);
        Assert.Equal(2, file.Rows.Count);
    }

    [Fact]
    public void Parse_AppliesASeparateDebitCreditIndicatorColumn()
    {
        var file = Parse(CsvFixture.AmountWithIndicator);

        Assert.Equal(CsvAmountConvention.AmountWithIndicator, file.AmountConvention);

        Assert.Equal(
            [-2400.00m, 7350.55m],
            file.Rows.Select(x => x.SignedAmount));
    }

    [Fact]
    public void Parse_ResolvesSlashDatesUsingADayAboveTwelve()
    {
        var dayFirst = Parse(CsvFixture.SlashDatesDayFirst);

        Assert.Equal(new DateOnly(2026, 6, 5), dayFirst.Rows[0].EffectiveDate);
        Assert.Equal(new DateOnly(2026, 6, 13), dayFirst.Rows[1].EffectiveDate);
        Assert.DoesNotContain(CsvParseWarning.AmbiguousDateFormat, dayFirst.Warnings);

        var monthFirst = Parse(CsvFixture.SlashDatesMonthFirst);

        Assert.Equal(new DateOnly(2026, 6, 5), monthFirst.Rows[0].EffectiveDate);
        Assert.Equal(new DateOnly(2026, 6, 13), monthFirst.Rows[1].EffectiveDate);
        Assert.DoesNotContain(CsvParseWarning.AmbiguousDateFormat, monthFirst.Warnings);
    }

    [Fact]
    public void Parse_WarnsWhenTheDateOrderCannotBeProved()
    {
        var file = Parse(CsvFixture.SlashDatesAmbiguous);

        Assert.Contains(CsvParseWarning.AmbiguousDateFormat, file.Warnings);
        Assert.Equal(new DateOnly(2026, 6, 5), file.Rows[0].EffectiveDate);
    }

    /// <summary>
    /// Rows that cannot be read are listed, not dropped. An import that reports "312 added"
    /// while three rows fell out silently is one the user will trust and a plan that is quietly
    /// short by three transactions.
    /// </summary>
    [Fact]
    public void Parse_ListsUnreadableRowsInsteadOfDroppingThemSilently()
    {
        var file = Parse(CsvFixture.UnreadableRows);

        Assert.Equal(2, file.Rows.Count);
        Assert.Equal(2, file.Issues.Count);

        Assert.Contains(
            file.Issues,
            issue => issue.Kind == CsvRowIssueKind.UnreadableAmount && issue.LineNumber == 3);

        Assert.Contains(
            file.Issues,
            issue => issue.Kind == CsvRowIssueKind.MissingDate && issue.LineNumber == 4);

        Assert.Contains(CsvParseWarning.SomeRowsCouldNotBeRead, file.Warnings);
    }

    /// <summary>
    /// A trailer line - a disclaimer, a section label - has neither a date nor an amount and is
    /// not a failed transaction. Reporting it would bury the rows that genuinely failed.
    /// </summary>
    [Fact]
    public void Parse_DoesNotReportTrailerLinesAsFailedRows()
    {
        var file = Parse(CsvFixture.UnreadableRows);

        Assert.DoesNotContain(file.Issues, issue => issue.LineNumber == 5);
    }

    [Fact]
    public void Parse_ThrowsAnActionableErrorForAFileThatIsNotTabular()
    {
        var exception = Assert.Throws<CsvParseException>(
            () => Parse(CsvFixture.Malformed));

        Assert.Contains("No column layout could be recognised", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ThrowsAnActionableErrorWhenTheDatesAreNotOneFormat()
    {
        var exception = Assert.Throws<CsvParseException>(
            () => Parse(CsvFixture.MalformedDates));

        Assert.Contains("dates in this file could not be read", exception.Message, StringComparison.Ordinal);

        // The message names the values it choked on, so the user can go and look at them.
        Assert.Contains("2026/02/30", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ThrowsForAnEmptyFile()
    {
        Assert.Throws<CsvParseException>(
            () => new CsvStatementParser().Parse([]));
    }

    /// <summary>
    /// A stated profile must reach the same answer as detection on a file it fits - otherwise
    /// picking a profile from the dropdown would change the numbers, which is the one thing it
    /// must never do.
    /// </summary>
    [Fact]
    public void Parse_UnderAMatchingProfileAgreesWithAutoDetection()
    {
        var detected = Parse(CsvFixture.SwissApostrophe);
        var stated = Parse(CsvFixture.SwissApostrophe, CsvStatementProfiles.SwissSemicolon);

        Assert.Equal(
            detected.Rows.Select(x => x.SignedAmount),
            stated.Rows.Select(x => x.SignedAmount));

        Assert.Equal(
            detected.Rows.Select(x => x.EffectiveDate),
            stated.Rows.Select(x => x.EffectiveDate));

        Assert.False(stated.WasAutoDetected);
        Assert.True(detected.WasAutoDetected);
    }

    /// <summary>
    /// The wrong profile must fail visibly, not quietly. Swiss amounts read under a German
    /// profile would otherwise turn -4.50 into -450, and a whole column failing is how the user
    /// finds out they picked the wrong one.
    /// </summary>
    [Fact]
    public void Parse_UnderTheWrongProfileFailsTheRowsRatherThanMisreadingThem()
    {
        var file = Parse(CsvFixture.SwissApostrophe, CsvStatementProfiles.GermanSemicolon);

        Assert.Empty(file.Rows);
        Assert.Equal(6, file.Issues.Count);
        Assert.All(file.Issues, issue => Assert.Equal(CsvRowIssueKind.UnreadableAmount, issue.Kind));
    }

    /// <summary>
    /// A cell carrying both separators reads the same under either profile, because "1.234,56"
    /// has exactly one meaning. Notation outranks the profile where notation is unambiguous.
    /// </summary>
    [Fact]
    public void Parse_ReadsBothSeparatorsCorrectlyEvenUnderTheOppositeProfile()
    {
        var file = Parse(CsvFixture.GermanFormat, CsvStatementProfiles.SwissSemicolon);

        Assert.Equal(
            [-2400.00m, -1234.56m, 7350.55m],
            file.Rows.Select(x => x.SignedAmount));
    }

    [Fact]
    public void LooksLikeCsv_AcceptsAStatementAndRejectsMt940()
    {
        Assert.True(
            CsvStatementParser.LooksLikeCsv(
                "Datum;Buchungstext;Betrag\r\n15.01.2026;Miete;-2'400.00\r\n"));

        Assert.False(
            CsvStatementParser.LooksLikeCsv(
                ":20:REF\n:25:CH2100210210108311400\n:60F:C260101CHF4042,62\n"));

        Assert.False(CsvStatementParser.LooksLikeCsv("just some prose, with a comma"));
    }
}
