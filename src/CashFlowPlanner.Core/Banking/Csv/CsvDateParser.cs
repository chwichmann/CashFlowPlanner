using System.Globalization;
using System.Text.RegularExpressions;

namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Reads a booking or value date out of a CSV cell, and works out the file's date format.
///
/// <para>
/// The dangerous case is <c>05/06/2026</c>: 5 June to a European reader, 6 May to an American
/// one, and there is nothing in that one cell to tell them apart. It is resolved the only way
/// it can be - by looking at the <b>whole column</b>. A candidate format is kept only if it
/// parses <i>every</i> date in the file, so a single <c>13/07/2026</c> anywhere eliminates
/// <c>M/d/yyyy</c>, and a single <c>07/13/2026</c> eliminates <c>d/M/yyyy</c>. That is the
/// "scan for a day greater than twelve" rule, expressed as a filter rather than as a special
/// case.
/// </para>
///
/// <para>
/// When both survive - a file whose every date has both components at twelve or below - the
/// day-first reading wins, because the app is Swiss, and
/// <see cref="Detection.IsAmbiguous"/> is set so the import can say so out loud instead of
/// quietly booking June's rent in May.
/// </para>
/// </summary>
public static class CsvDateParser
{
    /// <summary>
    /// Candidate formats, in preference order. Day-first always precedes month-first so that the
    /// tie-break falls the European way.
    ///
    /// <para>
    /// Single-letter <c>d</c> and <c>M</c> are used on purpose: when parsing they accept one
    /// <i>or</i> two digits, so <c>3.7.2026</c> and <c>03.07.2026</c> both match one entry.
    /// Doubling them would need twice the list and would still miss the mixed exports that pad
    /// some rows and not others.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> CandidateFormats =
    [
        "yyyy-M-d",
        "yyyy/M/d",
        "yyyyMMdd",
        "d.M.yyyy",
        "d/M/yyyy",
        "M/d/yyyy",
        "d-M-yyyy",
        "M-d-yyyy",
        "d.M.yy",
        "d/M/yy",
        "M/d/yy",
        "d MMM yyyy",
        "d MMMM yyyy"
    ];

    /// <summary>
    /// Pairs that differ only in whether the day or the month comes first. Nothing else in the
    /// candidate list can collide: no locale writes an American date with dots.
    /// </summary>
    private static readonly (string DayFirst, string MonthFirst)[] AmbiguousPairs =
    [
        ("d/M/yyyy", "M/d/yyyy"),
        ("d-M-yyyy", "M-d-yyyy"),
        ("d/M/yy", "M/d/yy")
    ];

    private static readonly Regex TrailingTimeRegex = new(
        @"[\sT]+\d{1,2}:\d{2}(:\d{2})?([.,]\d+)?\s*(Z|[+-]\d{2}:?\d{2})?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    public sealed record Detection(
        IReadOnlyList<string> Formats,
        bool IsAmbiguous)
    {
        public bool Succeeded =>
            Formats.Count > 0;

        public string? PrimaryFormat =>
            Formats.Count == 0 ? null : Formats[0];
    }

    /// <summary>
    /// Picks the format(s) that explain every date in the file.
    ///
    /// <paramref name="profileFormats"/> short-circuits the whole thing: a profile that states
    /// the format is trusted, because the alternative is re-deriving a fact the user already
    /// told us.
    /// </summary>
    public static Detection Detect(
        IEnumerable<string> samples,
        IReadOnlyList<string>? profileFormats = null)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (profileFormats is { Count: > 0 })
        {
            return new Detection(profileFormats, IsAmbiguous: false);
        }

        var normalized = samples
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .Where(x => x.Length > 0)
            .ToList();

        if (normalized.Count == 0)
        {
            return new Detection([], IsAmbiguous: false);
        }

        var survivors = CandidateFormats
            .Where(format => normalized.All(sample => MatchesExactly(sample, format)))
            .ToList();

        if (survivors.Count == 0)
        {
            return new Detection([], IsAmbiguous: false);
        }

        var isAmbiguous = AmbiguousPairs.Any(pair =>
            survivors.Contains(pair.DayFirst, StringComparer.Ordinal)
            && survivors.Contains(pair.MonthFirst, StringComparer.Ordinal));

        return new Detection(survivors, isAmbiguous);
    }

    public static bool TryParse(
        string? raw,
        IReadOnlyList<string> formats,
        out DateOnly value)
    {
        ArgumentNullException.ThrowIfNull(formats);

        value = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = Normalize(raw);

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(
                    normalized,
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                value = DateOnly.FromDateTime(parsed);
                return true;
            }
        }

        return false;
    }

    private static bool MatchesExactly(string normalizedSample, string format)
    {
        return DateTime.TryParseExact(
            normalizedSample,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    /// <summary>
    /// Strips what is not the date: surrounding whitespace, and a trailing clock time.
    ///
    /// Booking-date columns that carry a timestamp are common - "15.01.2026 00:00:00" and
    /// "2026-01-15T00:00:00" both occur - and the time is never information the plan uses.
    /// Removing it here keeps the candidate list from having to double for every format.
    /// </summary>
    private static string Normalize(string raw)
    {
        var trimmed = raw.Trim().Trim('"');

        var withoutTime = TrailingTimeRegex.Replace(trimmed, string.Empty);

        return withoutTime.Trim();
    }
}
