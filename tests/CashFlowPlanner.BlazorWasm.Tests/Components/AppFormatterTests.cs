using System.Globalization;
using CashFlowPlanner.BlazorWasm.Services;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// The twenty-one helpers AppFormatter replaces did not all agree, so what it produces has to be
/// pinned down rather than assumed.
/// </summary>
public sealed class AppFormatterTests
{
    private static readonly AppFormatter Formatter = new();

    [Theory]
    [InlineData("de-CH")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void Money_follows_the_current_culture_rather_than_a_hardcoded_one(string cultureName)
    {
        // HouseBuySimulator formatted through a hardcoded de-CH, so choosing en-US in Settings
        // changed every amount in the app except that page's.
        WithCulture(cultureName, () =>
            Assert.Equal(12345.60m.ToString("N2", CultureInfo.CurrentCulture), Formatter.Money(12345.60m)));
    }

    [Fact]
    public void Money_reads_the_culture_at_call_time_not_at_construction_time()
    {
        // Program.cs assigns the culture after the host is built, and Settings can change it again.
        // A formatter that captured CultureInfo.CurrentCulture in its constructor would be wrong
        // for the whole session.
        var formatter = new AppFormatter();

        var swiss = WithCulture("de-CH", () => formatter.Money(12345.60m));
        var american = WithCulture("en-US", () => formatter.Money(12345.60m));

        Assert.NotEqual(swiss, american);
    }

    [Fact]
    public void A_null_amount_is_a_dash_not_an_empty_cell()
    {
        // AccountStatement rendered string.Empty where every other page rendered "-", so an empty
        // cell there meant "no value" and everywhere else meant "no column".
        Assert.Equal("-", Formatter.Money((decimal?)null));
        Assert.Equal("-", Formatter.Date((DateOnly?)null));
        Assert.Equal("-", Formatter.Percent((decimal?)null));
    }

    [Fact]
    public void Currency_trails_the_amount()
    {
        WithCulture("de-CH", () => Assert.Equal($"{Formatter.Money(1200m)} CHF", Formatter.Money(1200m, "CHF")));
    }

    [Fact]
    public void A_blank_currency_leaves_no_dangling_space()
    {
        WithCulture("de-CH", () =>
        {
            Assert.Equal(Formatter.Money(1200m), Formatter.Money(1200m, null));
            Assert.Equal(Formatter.Money(1200m), Formatter.Money(1200m, "  "));
        });
    }

    [Fact]
    public void Percent_takes_a_percentage_not_a_fraction()
    {
        // The gap before the sign is a non-breaking space: a narrow column must not wrap the sign
        // onto its own line and leave a number that reads as an absolute amount.
        WithCulture("en-US", () => Assert.Equal("3.25\u00A0%", Formatter.Percent(3.25m)));
    }

    [Fact]
    public void Percent_can_be_asked_for_a_different_precision()
    {
        WithCulture("en-US", () => Assert.Equal("1.750\u00A0%", Formatter.Percent(1.75m, decimals: 3)));
    }

    [Fact]
    public void Number_covers_the_interest_rate_and_count_cases()
    {
        WithCulture("en-US", () =>
        {
            // Mortgages.FormatRate used three decimals and no unit at all.
            Assert.Equal("1.750", Formatter.Number(1.75m, decimals: 3));
            Assert.Equal("1,234", Formatter.Number(1234m, decimals: 0));
        });
    }

    [Fact]
    public void MoneyRounded_drops_the_decimals()
    {
        WithCulture("en-US", () => Assert.Equal("12,346", Formatter.MoneyRounded(12345.60m)));
    }

    [Theory]
    [InlineData("de-CH")]
    [InlineData("en-US")]
    public void Date_is_the_cultures_own_short_date(string cultureName)
    {
        var date = new DateOnly(2026, 8, 21);

        WithCulture(cultureName, () =>
            Assert.Equal(date.ToString("d", CultureInfo.CurrentCulture), Formatter.Date(date)));
    }

    [Fact]
    public void MonthLabel_turns_a_storage_key_into_a_heading()
    {
        WithCulture("en-US", () => Assert.Equal("August 2026", Formatter.MonthLabel("2026-08")));
    }

    [Fact]
    public void MonthLabel_reads_its_key_invariantly()
    {
        // "yyyy-MM" is MonthlyCashflowSummary.Label - a storage key, not display text. It must not
        // start meaning something else when the user switches region.
        var american = WithCulture("en-US", () => Formatter.MonthLabel("2026-03"));
        var german = WithCulture("de-DE", () => Formatter.MonthLabel("2026-03"));

        Assert.Equal("March 2026", american);
        Assert.Equal("M\u00E4rz 2026", german);
    }

    [Fact]
    public void MonthLabel_passes_an_unparseable_key_straight_through()
    {
        // What Simulation.FormatMonthLabel already did: a wrong-looking label beats an exception in
        // the middle of a results table.
        Assert.Equal("not-a-month", Formatter.MonthLabel("not-a-month"));
        Assert.Equal("-", Formatter.MonthLabel(null));
    }

    [Fact]
    public void DayOfWeek_is_localized()
    {
        var american = WithCulture("en-US", () => Formatter.DayOfWeek(System.DayOfWeek.Monday));
        var german = WithCulture("de-DE", () => Formatter.DayOfWeek(System.DayOfWeek.Monday));

        Assert.Equal("Monday", american);
        Assert.Equal("Montag", german);
    }

    private static void WithCulture(string cultureName, Action action) =>
        WithCulture(cultureName, () =>
        {
            action();
            return 0;
        });

    private static T WithCulture<T>(string cultureName, Func<T> action)
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
