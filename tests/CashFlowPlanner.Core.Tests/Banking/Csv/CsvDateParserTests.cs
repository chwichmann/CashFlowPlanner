using CashFlowPlanner.Core.Banking.Csv;

namespace CashFlowPlanner.Core.Tests.Banking.Csv;

public sealed class CsvDateParserTests
{
    [Theory]
    [InlineData("15.01.2026", "d.M.yyyy")]
    [InlineData("2026-01-15", "yyyy-M-d")]
    [InlineData("3.7.2026", "d.M.yyyy")]
    public void Detect_RecognisesTheUnambiguousFormats(string sample, string expectedFormat)
    {
        var detection = CsvDateParser.Detect([sample]);

        Assert.True(detection.Succeeded);
        Assert.Equal(expectedFormat, detection.PrimaryFormat);
    }

    /// <summary>
    /// The "scan the whole file for a day greater than twelve" rule. It is not a special case
    /// here - a candidate format survives only if it explains <i>every</i> date in the file, and
    /// 13 is not a month.
    /// </summary>
    [Fact]
    public void Detect_UsesADayAboveTwelveToProveTheDayComesFirst()
    {
        var detection = CsvDateParser.Detect(["05/06/2026", "13/06/2026"]);

        Assert.Equal("d/M/yyyy", detection.PrimaryFormat);
        Assert.False(detection.IsAmbiguous);
    }

    [Fact]
    public void Detect_UsesADayAboveTwelveInTheSecondPositionToProveTheMonthComesFirst()
    {
        var detection = CsvDateParser.Detect(["06/05/2026", "06/13/2026"]);

        Assert.Equal("M/d/yyyy", detection.PrimaryFormat);
        Assert.False(detection.IsAmbiguous);
    }

    /// <summary>
    /// When nothing in the file decides, the day-first reading wins - the app is Swiss - and the
    /// ambiguity is reported so the import can say so rather than quietly booking June's rent in
    /// May.
    /// </summary>
    [Fact]
    public void Detect_PrefersDayFirstAndSaysSoWhenNothingDecides()
    {
        var detection = CsvDateParser.Detect(["05/06/2026", "07/08/2026"]);

        Assert.Equal("d/M/yyyy", detection.PrimaryFormat);
        Assert.True(detection.IsAmbiguous);
    }

    [Fact]
    public void Detect_FailsWhenNoSingleFormatExplainsEveryValue()
    {
        var detection = CsvDateParser.Detect(["15.01.2026", "2026/02/30", "Januar"]);

        Assert.False(detection.Succeeded);
    }

    [Fact]
    public void Detect_TakesAStatedProfileFormatWithoutArguing()
    {
        var detection = CsvDateParser.Detect(["nonsense"], ["d.M.yyyy"]);

        Assert.Equal("d.M.yyyy", detection.PrimaryFormat);
    }

    [Theory]
    [InlineData("15.01.2026 00:00:00")]
    [InlineData("15.01.2026 14:35")]
    public void TryParse_IgnoresAClockTimeAfterTheDate(string raw)
    {
        Assert.True(CsvDateParser.TryParse(raw, ["d.M.yyyy"], out var value));
        Assert.Equal(new DateOnly(2026, 1, 15), value);
    }

    [Fact]
    public void TryParse_IgnoresAnIsoTimestampSuffix()
    {
        Assert.True(CsvDateParser.TryParse("2026-01-15T00:00:00", ["yyyy-M-d"], out var value));
        Assert.Equal(new DateOnly(2026, 1, 15), value);
    }

    [Fact]
    public void TryParse_RefusesWhatIsNotADate()
    {
        Assert.False(CsvDateParser.TryParse("Januar", ["d.M.yyyy"], out _));
    }
}
