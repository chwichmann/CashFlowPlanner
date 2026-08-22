using System.Globalization;
using System.Text;

namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Reads an amount out of a CSV cell.
///
/// <para>
/// <b>Never hands the text to <c>decimal.TryParse</c> with a culture and hopes.</b>
/// <see cref="NumberStyles.Number"/> permits group separators and does not check their spacing,
/// so under de-DE the string <c>"12.5"</c> parses successfully as <c>125</c> and under de-CH
/// <c>"5,25"</c> parses as <c>525</c>. Both are silent two-order-of-magnitude errors in a file
/// about somebody's money, and both are exactly the shapes Swiss bank exports contain.
/// </para>
///
/// <para>
/// So which character is the decimal point is decided <i>first</i> - by
/// <see cref="Detect"/> from the shape of every amount in the file, or by the profile - and only
/// then is the number parsed, invariantly, with the separators already resolved. This is the
/// same reasoning <c>Components/MoneyInput.razor</c> applies to typed input, moved from one
/// cell to a whole column: a file gives us hundreds of samples, and one unambiguous
/// <c>"12.50"</c> settles the reading of every ambiguous <c>"1.234"</c> next to it.
/// </para>
/// </summary>
public static class CsvAmountParser
{
    /// <summary>
    /// The separator the file uses, and whether the file actually said so.
    ///
    /// <see cref="IsAmbiguous"/> means the evidence ran out - every amount in the file was of the
    /// <c>1.234</c> shape, which is one thousand two hundred thirty-four to a German reader and
    /// just over one to a Swiss one. The reading is still chosen, but the import surfaces it as
    /// a warning instead of pretending to know.
    /// </summary>
    public sealed record Detection(CsvDecimalSeparator Separator, bool IsAmbiguous);

    /// <summary>
    /// Decides the decimal separator for a whole column from every value in it.
    ///
    /// <para>
    /// Evidence, strongest first: both <c>.</c> and <c>,</c> present means the last one is the
    /// decimal point; one of them repeated means that one is grouping; an apostrophe or space
    /// grouping the digits means any single <c>.</c> or <c>,</c> is the decimal point; and a
    /// separator followed by one, two or more than three digits is a decimal point, because no
    /// grouping produces those. Only a separator followed by exactly three digits carries no
    /// information, and a file made entirely of those is what <see cref="Detection.IsAmbiguous"/>
    /// reports.
    /// </para>
    /// </summary>
    public static Detection Detect(IEnumerable<string> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var dotVotes = 0;
        var commaVotes = 0;
        var dotIsGroupingHints = 0;
        var commaIsGroupingHints = 0;

        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample))
            {
                continue;
            }

            var hasExplicitGrouping = HasNonAmbiguousGrouping(sample);
            var cleaned = KeepSeparatorsAndDigits(sample);

            var dots = cleaned.Count(c => c == '.');
            var commas = cleaned.Count(c => c == ',');

            if (dots == 0 && commas == 0)
            {
                continue;
            }

            if (dots > 0 && commas > 0)
            {
                if (cleaned.LastIndexOf('.') > cleaned.LastIndexOf(','))
                {
                    dotVotes++;
                }
                else
                {
                    commaVotes++;
                }

                continue;
            }

            if (dots > 1)
            {
                // "1.234.567" - repeated, so grouping. Says nothing about the comma directly,
                // but it does rule the dot out.
                dotIsGroupingHints++;
                continue;
            }

            if (commas > 1)
            {
                commaIsGroupingHints++;
                continue;
            }

            var candidate = dots == 1 ? '.' : ',';
            var digitsAfter = cleaned.Length - cleaned.LastIndexOf(candidate) - 1;

            if (hasExplicitGrouping || digitsAfter is 1 or 2 || digitsAfter > 3)
            {
                if (candidate == '.')
                {
                    dotVotes++;
                }
                else
                {
                    commaVotes++;
                }

                continue;
            }

            if (digitsAfter == 3)
            {
                // The genuinely ambiguous shape. Counted, but only ever used when nothing
                // stronger turned up anywhere in the file.
                if (candidate == '.')
                {
                    dotIsGroupingHints++;
                }
                else
                {
                    commaIsGroupingHints++;
                }
            }
        }

        if (dotVotes > 0 || commaVotes > 0)
        {
            // A file that votes both ways is internally inconsistent - a bank mixing "1'234.50"
            // and "1.234,50" in one export. The majority wins and the ambiguity is reported.
            return new Detection(
                dotVotes >= commaVotes ? CsvDecimalSeparator.Dot : CsvDecimalSeparator.Comma,
                IsAmbiguous: dotVotes > 0 && commaVotes > 0);
        }

        if (dotIsGroupingHints > 0 && commaIsGroupingHints == 0)
        {
            // Only "1.234" shapes. Read the dot as grouping, which is the classic thousands
            // reading, and say so.
            return new Detection(CsvDecimalSeparator.Comma, IsAmbiguous: true);
        }

        if (commaIsGroupingHints > 0 && dotIsGroupingHints == 0)
        {
            return new Detection(CsvDecimalSeparator.Dot, IsAmbiguous: true);
        }

        if (dotIsGroupingHints > 0 && commaIsGroupingHints > 0)
        {
            return new Detection(CsvDecimalSeparator.Dot, IsAmbiguous: true);
        }

        // No separators anywhere: every amount is a whole number and the choice cannot matter.
        return new Detection(CsvDecimalSeparator.Dot, IsAmbiguous: false);
    }

    /// <summary>
    /// Parses one cell with the separator already decided.
    ///
    /// <para>
    /// Returns <c>false</c> rather than a wrong number when the cell contradicts the separator -
    /// a <c>"1,23"</c> read under a dot profile. A hundredfold error that imports quietly is far
    /// worse than a row the preview lists as unreadable, and a whole column failing that way is
    /// how a user discovers they picked the wrong profile.
    /// </para>
    /// </summary>
    public static bool TryParse(string? raw, CsvDecimalSeparator separator, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        var isNegative = IsNegative(trimmed);
        var cleaned = KeepSeparatorsAndDigits(trimmed);

        if (!cleaned.Any(char.IsDigit))
        {
            return false;
        }

        // When a cell carries both separators the last one is the decimal point and the other
        // groups the digits. That is a fact of notation rather than a convention - "1.234,56"
        // and "1,234.56" have exactly one reading each - so it outranks the profile. A user who
        // picked the Swiss profile for a German file still gets 2400.00 rather than a rejected
        // row, and nothing is guessed to get there.
        var bothSeparatorsPresent =
            cleaned.Contains('.', StringComparison.Ordinal)
            && cleaned.Contains(',', StringComparison.Ordinal);

        var decimalCharacter = bothSeparatorsPresent
            ? (cleaned.LastIndexOf('.') > cleaned.LastIndexOf(',') ? '.' : ',')
            : separator switch
            {
                CsvDecimalSeparator.Dot => '.',
                CsvDecimalSeparator.Comma => ',',
                _ => FindDecimalSeparator(cleaned, HasNonAmbiguousGrouping(trimmed))
            };

        var decimalIndex = decimalCharacter == '\0'
            ? -1
            : cleaned.LastIndexOf(decimalCharacter);

        if (decimalIndex < 0 && ContradictsSeparator(cleaned, decimalCharacter))
        {
            return false;
        }

        var builder = new StringBuilder(cleaned.Length + 1);

        for (var index = 0; index < cleaned.Length; index++)
        {
            var current = cleaned[index];

            if (char.IsDigit(current))
            {
                builder.Append(current);
                continue;
            }

            if (index == decimalIndex)
            {
                builder.Append('.');
            }

            // Any other '.' or ',' is grouping and is dropped.
        }

        if (!decimal.TryParse(
                builder.ToString(),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value))
        {
            value = 0m;
            return false;
        }

        if (isNegative)
        {
            value = -value;
        }

        return true;
    }

    /// <summary>
    /// True when the cell reads as a decimal under the <i>other</i> separator and not at all
    /// under this one - "1,23" against a dot profile.
    /// </summary>
    private static bool ContradictsSeparator(string cleaned, char decimalCharacter)
    {
        if (decimalCharacter == '\0')
        {
            return false;
        }

        var other = decimalCharacter == '.' ? ',' : '.';

        if (cleaned.Count(c => c == other) != 1)
        {
            return false;
        }

        var digitsAfter = cleaned.Length - cleaned.LastIndexOf(other) - 1;

        return digitsAfter is 1 or 2;
    }

    /// <summary>
    /// Negative signs come in four flavours in bank exports: a leading minus, a trailing minus
    /// (SAP-derived exports and several German ones), parentheses (spreadsheet round-trips), and
    /// the Unicode minus U+2212 that some PDFs-turned-CSV carry.
    /// </summary>
    private static bool IsNegative(string trimmed)
    {
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
        {
            return true;
        }

        return trimmed[0] is '-' or '−'
            || trimmed[^1] is '-' or '−';
    }

    /// <summary>
    /// Whether the digits are grouped by something that is unmistakably <i>not</i> a decimal
    /// point: an apostrophe of any shape, or a space of any width. When they are, whichever of
    /// '.' and ',' remains has to be the decimal point.
    /// </summary>
    private static bool HasNonAmbiguousGrouping(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            var isGrouping =
                current is '\'' or '\u2019' or '\u00b4' or '`'
                || current is ' ' or '\u00a0' or '\u202f' or '\u2009';

            // Only counts between digits - a trailing space or a quoted field's apostrophe is
            // not grouping.
            if (isGrouping
                && index > 0
                && index + 1 < value.Length
                && char.IsDigit(value[index - 1])
                && char.IsDigit(value[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Single-cell fallback for <see cref="CsvDecimalSeparator.Auto"/>, used only when a caller
    /// parses without having detected across the file first. Same shape rules as
    /// <see cref="Detect"/>, minus the cross-row evidence.
    /// </summary>
    private static char FindDecimalSeparator(string cleaned, bool hasExplicitGrouping)
    {
        var dots = cleaned.Count(c => c == '.');
        var commas = cleaned.Count(c => c == ',');

        if (dots == 0 && commas == 0)
        {
            return '\0';
        }

        if (dots > 0 && commas > 0)
        {
            return cleaned.LastIndexOf('.') > cleaned.LastIndexOf(',') ? '.' : ',';
        }

        if (dots > 1 || commas > 1)
        {
            return '\0';
        }

        var candidate = dots == 1 ? '.' : ',';

        if (hasExplicitGrouping)
        {
            return candidate;
        }

        var digitsAfter = cleaned.Length - cleaned.LastIndexOf(candidate) - 1;

        return digitsAfter == 3 ? '\0' : candidate;
    }

    private static string KeepSeparatorsAndDigits(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsDigit(character) || character is '.' or ',')
            {
                builder.Append(character);
            }

            // Currency codes and symbols, apostrophes, spaces of every width and the sign are
            // all dropped: "CHF 1'234.56-" has to work.
        }

        return builder.ToString();
    }
}
