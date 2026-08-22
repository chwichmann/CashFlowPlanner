using System.Globalization;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// The one place that turns numbers and dates into display strings.
///
/// Before this existed twenty-one private <c>FormatAmount</c>/<c>FormatDate</c>/<c>FormatMoney</c>/
/// <c>FormatRate</c> helpers were copy-pasted across twelve pages. Most agreed; several did not.
/// <c>AccountStatement</c> rendered an empty cell for a null amount where every other page rendered
/// <c>"-"</c>; <c>Persons</c> put the currency in front and hardcoded <c>CHF</c> while
/// <c>Pillar3a</c> and <c>BankImport</c> put it behind and took it as a parameter;
/// <c>HouseBuySimulator</c> formatted through a hardcoded <c>de-CH</c> and dropped the decimals, so
/// switching the region to <c>en-US</c> changed every amount on the app except that page's.
///
/// Everything here reads <see cref="CultureInfo.CurrentCulture"/> at call time, never at
/// construction time: <c>Program.cs</c> assigns the culture after the host is built, and the user
/// can change the region in Settings.
/// </summary>
public sealed class AppFormatter
{
    /// <summary>
    /// What a null or absent value renders as. The majority of pages already used this; the
    /// migration should replace <c>AccountStatement</c>'s empty string with it rather than the
    /// other way round, so an empty cell always means "no column", never "no value".
    /// </summary>
    public const string EmptyMarker = "-";

    private const string MoneyFormat = "N2";
    private const string DateFormat = "d";
    private const string MonthFormat = "Y";

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    /// <summary>An amount without a currency, e.g. <c>12'345.60</c>.</summary>
    public string Money(decimal value) => value.ToString(MoneyFormat, Culture);

    /// <summary>An amount without a currency, or <see cref="EmptyMarker"/>.</summary>
    public string Money(decimal? value) => value is null ? EmptyMarker : Money(value.Value);

    /// <summary>
    /// An amount followed by its currency, e.g. <c>12'345.60 CHF</c>. The currency trails the
    /// number because that is what nine of the ten existing call sites did; a blank currency
    /// degrades to the bare amount rather than leaving a dangling space.
    /// </summary>
    public string Money(decimal value, string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? Money(value) : $"{Money(value)} {currency}";

    /// <summary>An amount with its currency, or <see cref="EmptyMarker"/>.</summary>
    public string Money(decimal? value, string? currency) =>
        value is null ? EmptyMarker : Money(value.Value, currency);

    /// <summary>
    /// A whole-franc amount, e.g. <c>12'346</c>. HouseBuySimulator rounds its inputs to whole
    /// currency units on purpose - a mortgage scenario in rappen is noise.
    /// </summary>
    public string MoneyRounded(decimal value) => value.ToString("N0", Culture);

    /// <summary>
    /// A percentage, e.g. <c>3.25 %</c>. The value is already a percentage, not a fraction: pass
    /// <c>3.25</c>, not <c>0.0325</c>.
    ///
    /// A space before the sign follows the two existing localized pages - HouseBuySimulator's
    /// space-less variant is the odd one out and changes when it migrates - and it is a
    /// non-breaking one, so a narrow table column cannot wrap the sign onto its own line and leave
    /// a number that reads as an absolute amount.
    /// </summary>
    public string Percent(decimal value, int decimals = 2) =>
        $"{Number(value, decimals)}\u00A0%";

    /// <summary>A percentage, or <see cref="EmptyMarker"/>.</summary>
    public string Percent(decimal? value, int decimals = 2) =>
        value is null ? EmptyMarker : Percent(value.Value, decimals);

    /// <summary>
    /// A plain number with a fixed number of decimals and thousands separators. Interest rates use
    /// three decimals (<c>Mortgages.FormatRate</c>), counts use none.
    /// </summary>
    public string Number(decimal value, int decimals = 2) =>
        value.ToString(
            decimals <= 0 ? "N0" : "N" + decimals.ToString(CultureInfo.InvariantCulture),
            Culture);

    /// <summary>A short date in the current culture's own order, e.g. <c>21.08.2026</c>.</summary>
    public string Date(DateOnly value) => value.ToString(DateFormat, Culture);

    /// <summary>A short date, or <see cref="EmptyMarker"/>.</summary>
    public string Date(DateOnly? value) => value is null ? EmptyMarker : Date(value.Value);

    /// <summary>A short date from a <see cref="DateTime"/>.</summary>
    public string Date(DateTime value) => value.ToString(DateFormat, Culture);

    /// <summary>A short date and time, for "last saved at" style timestamps.</summary>
    public string DateTime(DateTimeOffset value) => value.LocalDateTime.ToString("g", Culture);

    /// <summary>The current culture's name for a day of the week.</summary>
    public string DayOfWeek(DayOfWeek value) => Culture.DateTimeFormat.GetDayName(value);

    /// <summary>
    /// A month heading, e.g. <c>August 2026</c>, from a <c>yyyy-MM</c> key such as
    /// <c>MonthlyCashflowSummary.Label</c> produces. An unparseable key is passed through
    /// untouched, which is what the simulation page already did - a wrong-looking label beats an
    /// exception in the middle of a results table.
    /// </summary>
    public string MonthLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return EmptyMarker;
        }

        return TryParseMonthKey(label, out var year, out var month)
            ? MonthLabel(year, month)
            : label;
    }

    /// <summary>A month heading from an explicit year and month.</summary>
    public string MonthLabel(int year, int month) =>
        new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified).ToString(MonthFormat, Culture);

    /// <summary>A month heading from any date in that month.</summary>
    public string MonthLabel(DateOnly value) => MonthLabel(value.Year, value.Month);

    private static bool TryParseMonthKey(string label, out int year, out int month)
    {
        year = 0;
        month = 0;

        // Parsed with the invariant culture on purpose: "yyyy-MM" is a storage key, not display
        // text, and must not start meaning something else when the user switches region.
        if (!System.DateTime.TryParseExact(
                label,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        year = parsed.Year;
        month = parsed.Month;

        return true;
    }
}
