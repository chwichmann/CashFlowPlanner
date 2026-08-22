using CashFlowPlanner.Core.Indexation;
using System.Globalization;

namespace CashFlowPlanner.Core.Tests.Indexation;

public sealed class AnnualCompoundingTests
{
    [Theory]
    // Same day, and every day up to the day before the first anniversary.
    [InlineData("2026-01-01", "2026-01-01", 0)]
    [InlineData("2026-01-01", "2026-12-31", 0)]
    // The anniversary itself is the first completed year.
    [InlineData("2026-01-01", "2027-01-01", 1)]
    [InlineData("2026-01-01", "2027-06-30", 1)]
    [InlineData("2026-01-01", "2046-01-01", 20)]
    // Backwards: less than a full year back is still 0.
    [InlineData("2026-01-01", "2025-06-30", 0)]
    [InlineData("2026-01-01", "2025-01-01", -1)]
    [InlineData("2026-01-01", "2024-12-31", -1)]
    [InlineData("2026-01-01", "2024-01-01", -2)]
    // 29 February maps onto 28 February, so a leap-day base date still steps
    // exactly once a year.
    [InlineData("2024-02-29", "2025-02-28", 1)]
    [InlineData("2024-02-29", "2025-02-27", 0)]
    public void CompletedYears_CountsWholeYearsFromTheBaseDate(
        string baseDate,
        string date,
        int expected)
    {
        var years = AnnualCompounding.CompletedYears(
            DateOnly.Parse(baseDate),
            DateOnly.Parse(date));

        Assert.Equal(expected, years);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0, 40, 0)]
    [InlineData(2, 0, 1)]
    [InlineData(3, 1, 3)]
    [InlineData(3, 2, 9)]
    [InlineData(2, 10, 1024)]
    [InlineData(-2, 3, -8)]
    public void Pow_IsExactForIntegerExponents(
        int value,
        int exponent,
        int expected)
    {
        Assert.Equal(expected, AnnualCompounding.Pow(value, exponent));
    }

    [Fact]
    public void Pow_NegativeExponent_Inverts()
    {
        Assert.Equal(0.25m, AnnualCompounding.Pow(2m, -2));
    }

    [Fact]
    public void Factor_ZeroRate_IsExactlyOne()
    {
        var factor = AnnualCompounding.Factor(
            0m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2056, 1, 1));

        Assert.Equal(1m, factor);
    }

    /// <summary>
    /// The whole point of gap 2: a monthly expense stated in 2026 money is
    /// understated by a third after 20 years at 1.5%, and by 81% after 40 at 2%.
    /// </summary>
    [Theory]
    [InlineData("1.5", 20, "1346.8550065500560376005930178")]
    [InlineData("2.0", 30, "1811.3615841033537550568104992")]
    public void Index_CompoundsAnnually(
        string ratePercent,
        int years,
        string expected)
    {
        var baseDate = new DateOnly(2026, 1, 1);

        var indexed = AnnualCompounding.Index(
            1_000m,
            decimal.Parse(ratePercent, CultureInfo.InvariantCulture),
            baseDate,
            baseDate.AddYears(years));

        Assert.Equal(
            decimal.Parse(expected, CultureInfo.InvariantCulture),
            indexed,
            10);
    }

    /// <summary>
    /// Indexing then deflating has to return the original amount exactly, or
    /// "real terms" and "nominal terms" would not be two views of one number.
    /// </summary>
    [Theory]
    [InlineData("1.5", 20)]
    [InlineData("2.0", 30)]
    [InlineData("-1.0", 10)]
    public void Deflate_IsTheInverseOfIndex(string ratePercent, int years)
    {
        var rate = decimal.Parse(ratePercent, CultureInfo.InvariantCulture);
        var baseDate = new DateOnly(2026, 1, 1);
        var date = baseDate.AddYears(years);

        var indexed = AnnualCompounding.Index(1_234.56m, rate, baseDate, date);
        var deflated = AnnualCompounding.Deflate(indexed, rate, baseDate, date);

        Assert.Equal(1_234.56m, deflated, 8);
    }

    /// <summary>
    /// Compounding happens once a year on the anniversary, not on every
    /// occurrence. Twelve monthly charges in the same indexation year all carry
    /// the same amount.
    /// </summary>
    [Fact]
    public void Index_DoesNotCompoundPerOccurrence()
    {
        var baseDate = new DateOnly(2026, 1, 1);

        var amounts = Enumerable
            .Range(0, 12)
            .Select(month => AnnualCompounding.Index(
                1_000m,
                2m,
                baseDate,
                baseDate.AddMonths(month)))
            .Distinct()
            .ToList();

        Assert.Equal([1_000m], amounts);
    }
}
