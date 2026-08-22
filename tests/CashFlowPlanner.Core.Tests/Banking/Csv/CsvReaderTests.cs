using CashFlowPlanner.Core.Banking.Csv;

namespace CashFlowPlanner.Core.Tests.Banking.Csv;

/// <summary>
/// The RFC 4180 mechanics. Everything here is something <c>text.Split(';')</c> gets wrong, and
/// gets wrong by turning one transaction into two or by cutting a payee's name in half.
/// </summary>
public sealed class CsvReaderTests
{
    [Fact]
    public void Read_KeepsDelimiterInsideQuotedField()
    {
        var result = CsvReader.Read("a;\"b;c\";d", ';');

        var row = Assert.Single(result.Rows);

        Assert.Equal(["a", "b;c", "d"], row.Fields);
    }

    [Fact]
    public void Read_KeepsNewlineInsideQuotedField_AndReportsTheStartingLine()
    {
        var result = CsvReader.Read("h1;h2\r\na;\"line one\nline two\"\r\nb;c", ';');

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal("line one\nline two", result.Rows[1].Fields[1]);

        // The record starts on line 2 and spans two physical lines, so the record after it is on
        // line 4. A preview that says "row 4 could not be read" has to mean line 4 in an editor.
        Assert.Equal(2, result.Rows[1].LineNumber);
        Assert.Equal(4, result.Rows[2].LineNumber);
    }

    [Fact]
    public void Read_TreatsDoubledQuoteAsOneLiteralQuote()
    {
        var result = CsvReader.Read("\"say \"\"hello\"\"\";x", ';');

        var row = Assert.Single(result.Rows);

        Assert.Equal("say \"hello\"", row.Fields[0]);
    }

    [Theory]
    [InlineData("a;b\r\nc;d")]
    [InlineData("a;b\nc;d")]
    [InlineData("a;b\rc;d")]
    public void Read_AcceptsEveryLineEnding(string text)
    {
        var result = CsvReader.Read(text, ';');

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(["c", "d"], result.Rows[1].Fields);
    }

    [Fact]
    public void Read_SkipsBlankLines()
    {
        var result = CsvReader.Read("a;b\r\n\r\nc;d\r\n\r\n", ';');

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Read_KeepsEmptyFields()
    {
        var result = CsvReader.Read("a;;c", ';');

        var row = Assert.Single(result.Rows);

        Assert.Equal(["a", "", "c"], row.Fields);
    }

    [Fact]
    public void Read_ReportsAnUnterminatedQuoteWithoutThrowing()
    {
        var result = CsvReader.Read("a;\"never closed\r\nb;c", ';');

        Assert.True(result.HasUnterminatedQuote);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void Read_KeepsTheRawRecordText()
    {
        var result = CsvReader.Read("a;\"b;c\"\r\n", ';');

        var row = Assert.Single(result.Rows);

        Assert.Equal("a;\"b;c\"", row.RawText);
    }
}
