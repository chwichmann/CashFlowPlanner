using CashFlowPlanner.Core.Banking.Csv;

namespace CashFlowPlanner.Core.Tests.Banking.Csv;

/// <summary>
/// The number reading, tested at the exact places where handing the text to
/// <c>decimal.TryParse</c> with a culture produces a silent hundredfold error.
/// </summary>
public sealed class CsvAmountParserTests
{
    [Theory]
    [InlineData("1'234.56", 1234.56)]
    [InlineData("-2'400.00", -2400.00)]
    [InlineData("12'345'678.90", 12345678.90)]
    [InlineData("4.50", 4.50)]
    [InlineData("0.05", 0.05)]
    [InlineData("CHF 1'234.56", 1234.56)]
    [InlineData("1234", 1234)]
    public void TryParse_ReadsSwissAmounts(string raw, double expected)
    {
        Assert.True(CsvAmountParser.TryParse(raw, CsvDecimalSeparator.Dot, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("-2.400,00", -2400.00)]
    [InlineData("12,50", 12.50)]
    [InlineData("1.234.567,89", 1234567.89)]
    public void TryParse_ReadsGermanAmounts(string raw, double expected)
    {
        Assert.True(CsvAmountParser.TryParse(raw, CsvDecimalSeparator.Comma, out var value));
        Assert.Equal((decimal)expected, value);
    }

    /// <summary>
    /// The failure this whole class exists to prevent. Under de-DE,
    /// <c>decimal.TryParse("12.5", NumberStyles.Number, ...)</c> succeeds and returns 125,
    /// because group separators are allowed and their spacing is not checked.
    /// </summary>
    [Fact]
    public void TryParse_DoesNotTurnTwelveFiftyIntoOneHundredTwentyFive()
    {
        Assert.True(CsvAmountParser.TryParse("12.5", CsvDecimalSeparator.Dot, out var value));
        Assert.Equal(12.5m, value);
    }

    [Theory]
    [InlineData("1234.56-", -1234.56)]
    [InlineData("(1234.56)", -1234.56)]
    [InlineData("-1234.56", -1234.56)]
    public void TryParse_ReadsEverySignConvention(string raw, double expected)
    {
        Assert.True(CsvAmountParser.TryParse(raw, CsvDecimalSeparator.Dot, out var value));
        Assert.Equal((decimal)expected, value);
    }

    /// <summary>
    /// A German amount read under a Swiss profile must fail loudly rather than come back a
    /// hundred times too big. A whole column failing this way is how a user finds out they
    /// picked the wrong profile; a column of silently wrong numbers is how they do not.
    /// </summary>
    [Fact]
    public void TryParse_RefusesACellThatContradictsTheSeparator()
    {
        Assert.False(CsvAmountParser.TryParse("1'234,56", CsvDecimalSeparator.Dot, out _));
        Assert.False(CsvAmountParser.TryParse("1'234.56", CsvDecimalSeparator.Comma, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n/a")]
    [InlineData("-")]
    public void TryParse_RefusesWhatIsNotANumber(string raw)
    {
        Assert.False(CsvAmountParser.TryParse(raw, CsvDecimalSeparator.Dot, out _));
    }

    [Fact]
    public void TryParse_ReadsAThousandsSeparatorThatIsNotTheDecimalPoint()
    {
        // "1,234" under a dot profile is one thousand two hundred thirty-four.
        Assert.True(CsvAmountParser.TryParse("1,234", CsvDecimalSeparator.Dot, out var value));
        Assert.Equal(1234m, value);
    }

    [Fact]
    public void Detect_UsesApostropheGroupingToProveTheDotIsTheDecimalPoint()
    {
        var detection = CsvAmountParser.Detect(["1'234.567"]);

        Assert.Equal(CsvDecimalSeparator.Dot, detection.Separator);
        Assert.False(detection.IsAmbiguous);
    }

    [Fact]
    public void Detect_UsesBothSeparatorsToDecideWhichIsTheDecimalPoint()
    {
        Assert.Equal(
            CsvDecimalSeparator.Comma,
            CsvAmountParser.Detect(["1.234,56"]).Separator);

        Assert.Equal(
            CsvDecimalSeparator.Dot,
            CsvAmountParser.Detect(["1,234.56"]).Separator);
    }

    /// <summary>
    /// One unambiguous neighbour settles the whole column. This is what a file gives us that a
    /// single input box does not.
    /// </summary>
    [Fact]
    public void Detect_LetsOneUnambiguousValueSettleTheAmbiguousOnes()
    {
        var detection = CsvAmountParser.Detect(["1.234", "4.50", "2.100"]);

        Assert.Equal(CsvDecimalSeparator.Dot, detection.Separator);
        Assert.False(detection.IsAmbiguous);
    }

    [Fact]
    public void Detect_SaysSoWhenTheFileNeverSettlesIt()
    {
        // Every value is "n.nnn". A German reader and a Swiss one disagree, and nothing in the
        // file decides. The reading is still chosen - but it is reported as a guess.
        var detection = CsvAmountParser.Detect(["1.234", "2.500"]);

        Assert.True(detection.IsAmbiguous);
    }

    [Fact]
    public void Detect_TreatsARepeatedSeparatorAsGrouping()
    {
        var detection = CsvAmountParser.Detect(["1.234.567"]);

        Assert.Equal(CsvDecimalSeparator.Comma, detection.Separator);
    }
}
