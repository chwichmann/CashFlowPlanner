using System.Globalization;
using System.Text;

namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Turns a header row into a <see cref="CsvColumnMapping"/>.
///
/// <para>
/// Matching is scored rather than first-wins, because first-wins gets
/// <c>Valutadatum</c> wrong: it contains <c>datum</c>, and a naive scan assigns it the booking
/// date while the real booking-date column sits unclaimed two columns to the left. An exact
/// header beats a whole-word match, a whole-word match beats a substring, and among equals the
/// longer alias wins - which is precisely what makes <c>Valutadatum</c> resolve to
/// <c>valuta</c> (6) rather than <c>datum</c> (5).
/// </para>
///
/// <para>
/// Assignment is then a greedy pass over the scores, one column per role and one role per
/// column, so two columns that both look like the description cannot both claim it and leave
/// the counterparty empty.
/// </para>
/// </summary>
public static class CsvColumnMapper
{
    /// <summary>
    /// Below this length an alias only ever matches exactly or as a whole word. <c>id</c>,
    /// <c>sh</c> and <c>art</c> as substrings would match half the headers in any export.
    /// </summary>
    private const int MinimumSubstringAliasLength = 5;

    private const int ExactScore = 1_000_000;
    private const int WholeWordScore = 10_000;
    private const int SubstringScore = 100;

    /// <summary>An alias with its normalisation and tokenisation done once, at type load.</summary>
    private sealed record NormalizedAlias(string Text, IReadOnlyList<string> Tokens);

    /// <summary>
    /// The two hundred-odd built-in aliases, normalised once.
    ///
    /// <para>
    /// Not a micro-optimisation. Finding the header means resolving up to forty candidate rows
    /// against four candidate delimiters, and the content sniff does it again - so a normalisation
    /// done per comparison runs hundreds of thousands of times per upload, each one a
    /// <c>Normalize(FormD)</c> and a <see cref="StringBuilder"/>. That is free on a desktop and
    /// distinctly not free in a WebAssembly runtime on a phone, which is where this app runs.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<CsvColumnRole, IReadOnlyList<NormalizedAlias>>
        DefaultNormalizedAliases = NormalizeAliases(CsvColumnAliases.Default);

    public static CsvColumnMapping Resolve(
        IReadOnlyList<string> headers,
        CsvStatementProfile profile)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(profile);

        var aliases = BuildAliases(profile);

        var candidates = new List<(int Column, CsvColumnRole Role, int Score)>();

        for (var column = 0; column < headers.Count; column++)
        {
            var normalizedHeader = NormalizeHeader(headers[column]);

            if (normalizedHeader.Length == 0)
            {
                continue;
            }

            var headerTokens = Tokenize(normalizedHeader);

            foreach (var (role, roleAliases) in aliases)
            {
                var score = ScoreRole(normalizedHeader, headerTokens, roleAliases);

                if (score > 0)
                {
                    candidates.Add((column, role, score));
                }
            }
        }

        var assigned = new Dictionary<CsvColumnRole, int>();
        var usedColumns = new HashSet<int>();

        foreach (var candidate in candidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Column)
            .ThenBy(x => (int)x.Role))
        {
            if (assigned.ContainsKey(candidate.Role) || usedColumns.Contains(candidate.Column))
            {
                continue;
            }

            assigned[candidate.Role] = candidate.Column;
            usedColumns.Add(candidate.Column);
        }

        // Explicit indexes win over anything inferred: a profile that pins a column is a user
        // or an author stating a fact, and inference must not argue with it.
        foreach (var (role, index) in profile.ColumnIndexOverrides)
        {
            if (index >= 0)
            {
                assigned[role] = index;
            }
        }

        return new CsvColumnMapping
        {
            ColumnIndexByRole = assigned,
            Headers = headers
        };
    }

    private static IReadOnlyDictionary<CsvColumnRole, IReadOnlyList<NormalizedAlias>> BuildAliases(
        CsvStatementProfile profile)
    {
        if (profile.ColumnHeaderOverrides.Count == 0)
        {
            return DefaultNormalizedAliases;
        }

        var merged = new Dictionary<CsvColumnRole, IReadOnlyList<NormalizedAlias>>(
            DefaultNormalizedAliases);

        foreach (var (role, extraAliases) in profile.ColumnHeaderOverrides)
        {
            // Profile aliases are prepended, not substituted: an author naming their bank's
            // odd header does not want to lose the twenty ordinary spellings alongside it.
            var normalizedExtras = NormalizeAliasList(extraAliases);

            merged[role] = merged.TryGetValue(role, out var existing)
                ? normalizedExtras.Concat(existing).ToList()
                : normalizedExtras;
        }

        return merged;
    }

    private static IReadOnlyDictionary<CsvColumnRole, IReadOnlyList<NormalizedAlias>> NormalizeAliases(
        IReadOnlyDictionary<CsvColumnRole, IReadOnlyList<string>> aliases)
    {
        return aliases.ToDictionary(
            x => x.Key,
            x => NormalizeAliasList(x.Value));
    }

    private static IReadOnlyList<NormalizedAlias> NormalizeAliasList(IReadOnlyList<string> aliases)
    {
        return aliases
            .Select(NormalizeHeader)
            .Where(x => x.Length > 0)
            .Select(x => new NormalizedAlias(x, Tokenize(x)))
            .ToList();
    }

    private static int ScoreRole(
        string normalizedHeader,
        IReadOnlyList<string> headerTokens,
        IReadOnlyList<NormalizedAlias> roleAliases)
    {
        var best = 0;

        foreach (var alias in roleAliases)
        {
            int score;

            if (string.Equals(normalizedHeader, alias.Text, StringComparison.Ordinal))
            {
                score = ExactScore + alias.Text.Length;
            }
            else if (ContainsTokenSequence(headerTokens, alias.Tokens))
            {
                score = WholeWordScore + alias.Text.Length;
            }
            else if (alias.Text.Length >= MinimumSubstringAliasLength
                && normalizedHeader.Contains(alias.Text, StringComparison.Ordinal))
            {
                score = SubstringScore + alias.Text.Length;
            }
            else
            {
                continue;
            }

            if (score > best)
            {
                best = score;
            }
        }

        return best;
    }

    private static bool ContainsTokenSequence(
        IReadOnlyList<string> headerTokens,
        IReadOnlyList<string> aliasTokens)
    {
        if (aliasTokens.Count == 0 || aliasTokens.Count > headerTokens.Count)
        {
            return false;
        }

        for (var start = 0; start <= headerTokens.Count - aliasTokens.Count; start++)
        {
            var matched = true;

            for (var offset = 0; offset < aliasTokens.Count; offset++)
            {
                if (!string.Equals(
                        headerTokens[start + offset],
                        aliasTokens[offset],
                        StringComparison.Ordinal))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Lower-cases, strips diacritics and collapses whitespace, so <c>Débit</c>, <c>DEBIT</c>
    /// and <c>debit</c> are one string.
    ///
    /// Diacritic stripping deliberately does not expand umlauts (<c>ä</c> becomes <c>a</c>, not
    /// <c>ae</c>) - both spellings are carried explicitly in
    /// <see cref="CsvColumnAliases.Default"/> instead, because a bank that writes
    /// <c>Empfaenger</c> and one that writes <c>Empfänger</c> are equally common and neither
    /// transliteration rule catches both.
    /// </summary>
    public static string NormalizeHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);

        var trimmed = header.Trim().Trim('"', '\'', ':', '*', '#');

        var decomposed = trimmed.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            lastWasSpace = false;
            builder.Append(char.ToLowerInvariant(character));
        }

        return builder
            .ToString()
            .TrimEnd()
            .Normalize(NormalizationForm.FormC);
    }

    private static IReadOnlyList<string> Tokenize(string normalizedHeader)
    {
        return normalizedHeader
            .Split(
                [' ', '/', '\\', '-', '_', '(', ')', '[', ']', ',', '.', ':', ';', '+'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
