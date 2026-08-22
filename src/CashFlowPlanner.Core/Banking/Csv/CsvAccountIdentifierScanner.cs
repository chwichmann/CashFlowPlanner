using System.Text;
using System.Text.RegularExpressions;

namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Finds the account's IBAN in the preamble a CSV export puts above its header.
///
/// <para>
/// CSV has no <c>Acct/Id/IBAN</c>. What it usually has instead is two or three lines of prose
/// before the header - account number, holder, date range, currency - and the IBAN is in there
/// often enough to be worth looking for: finding it means the statement matches an account
/// automatically, the same way a camt.053 statement does, instead of making the user pick from
/// a dropdown every month.
/// </para>
///
/// <para>
/// The check-digit test is what makes this safe to do. A bare regex for "two letters, two
/// digits, then alphanumerics" also matches Swiss QR reference numbers, booking references and
/// half the transaction ids in the file; validating mod-97 rejects all of those. A false
/// positive here would silently match the statement to the wrong account, which is worse than
/// not matching at all.
/// </para>
/// </summary>
public static class CsvAccountIdentifierScanner
{
    private static readonly Regex CandidateRegex = new(
        @"\b[A-Z]{2}[0-9]{2}(?:[ ]?[A-Z0-9]){10,32}\b",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    private static readonly Regex CurrencyRegex = new(
        @"\b(CHF|EUR|USD|GBP|JPY)\b",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    public static string? FindIban(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            foreach (Match match in CandidateRegex.Matches(line.ToUpperInvariant()))
            {
                var candidate = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal);

                if (IsValidIban(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static string? FindCurrency(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = CurrencyRegex.Match(line.ToUpperInvariant());

            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// ISO 13616 check: move the first four characters to the end, replace letters by their
    /// position in the alphabet plus nine, and require the resulting number to be 1 mod 97.
    /// Done digit by digit so no 100-digit number has to be built.
    /// </summary>
    public static bool IsValidIban(string candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.Length is < 15 or > 34)
        {
            return false;
        }

        if (!char.IsAsciiLetterUpper(candidate[0])
            || !char.IsAsciiLetterUpper(candidate[1])
            || !char.IsAsciiDigit(candidate[2])
            || !char.IsAsciiDigit(candidate[3]))
        {
            return false;
        }

        var rearranged = new StringBuilder(candidate.Length + 4);

        rearranged.Append(candidate, 4, candidate.Length - 4);
        rearranged.Append(candidate, 0, 4);

        var remainder = 0;

        foreach (var character in rearranged.ToString())
        {
            int value;

            if (char.IsAsciiDigit(character))
            {
                value = character - '0';
                remainder = ((remainder * 10) + value) % 97;
                continue;
            }

            if (!char.IsAsciiLetterUpper(character))
            {
                return false;
            }

            value = character - 'A' + 10;
            remainder = ((remainder * 100) + value) % 97;
        }

        return remainder == 1;
    }
}
