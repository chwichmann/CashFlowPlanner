using System.Text.RegularExpressions;

namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Parses a bank statement exported as CSV, under a <see cref="CsvStatementProfile"/>.
///
/// <para>
/// CSV is the always-available path for a Swiss private customer. camt.053 is a
/// business-banking feature at most retail banks and does not exist at all at the neobanks, so
/// for several banks this is not the convenient import - it is the only one. That is why this
/// parser is built to be shown rather than trusted: every inference it makes is recorded on
/// <see cref="CsvStatementFile"/> and rendered before anything is committed.
/// </para>
///
/// <para>
/// The order of work matters and is not arbitrary. Encoding, then delimiter, then the header
/// row, then the column roles, then - only once the amount and date <i>columns</i> are known -
/// the decimal separator and the date format, decided from every value in those columns at
/// once. Deciding a number format per cell is how <c>"12.5"</c> becomes 125; deciding it per
/// column is how one unambiguous neighbour settles it.
/// </para>
/// </summary>
public sealed class CsvStatementParser
{
    /// <summary>Delimiters tried when a profile does not state one, in order of plausibility for a Swiss export.</summary>
    private static readonly char[] CandidateDelimiters = [';', ',', '\t', '|'];

    /// <summary>How much of the file the delimiter sniff looks at. Enough for a preamble and a header.</summary>
    private const int DelimiterDetectionLength = 65_536;

    private static readonly Regex Mt940MarkerRegex = new(
        @"(^|\n)\s*(\{1:|\{4:|:20:|:25:|:60F:|:61:|:86:)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    public CsvStatementFile Parse(byte[] bytes, CsvStatementProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var effectiveProfile = profile ?? CsvStatementProfiles.Auto;

        var decoded = CsvTextDecoder.Decode(bytes, effectiveProfile.Encoding);

        return Parse(decoded.Text, effectiveProfile, decoded.Encoding);
    }

    public CsvStatementFile Parse(
        string text,
        CsvStatementProfile? profile = null,
        CsvTextEncoding encoding = CsvTextEncoding.Utf8)
    {
        ArgumentNullException.ThrowIfNull(text);

        var effectiveProfile = profile ?? CsvStatementProfiles.Auto;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new CsvParseException("The CSV file is empty.");
        }

        var delimiter = effectiveProfile.Delimiter ?? DetectDelimiter(text, effectiveProfile);

        var read = CsvReader.Read(text, delimiter, effectiveProfile.Quote);

        if (read.Rows.Count == 0)
        {
            throw new CsvParseException("The CSV file contains no rows.");
        }

        var header = FindHeader(read.Rows, effectiveProfile, delimiter);

        var dataRows = read.Rows
            .Skip(header.RowIndex + 1)
            .Where(row => !row.IsEmpty)
            .ToList();

        var convention = ResolveAmountConvention(effectiveProfile, header.Mapping);

        var decimalSeparator = ResolveDecimalSeparator(
            effectiveProfile,
            header.Mapping,
            dataRows,
            out var decimalSeparatorIsAmbiguous);

        var dateDetection = ResolveDateFormats(effectiveProfile, header.Mapping, dataRows);

        var warnings = new List<CsvParseWarning>();
        var issues = new List<CsvRowIssue>();

        var rows = ParseRows(
            dataRows,
            header,
            effectiveProfile,
            convention,
            decimalSeparator,
            dateDetection.Formats,
            issues);

        var preambleLines = read.Rows
            .Take(Math.Max(0, header.RowIndex))
            .Select(row => row.RawText)
            .ToList();

        var currency = ResolveFileCurrency(
            rows,
            preambleLines,
            effectiveProfile,
            out var hasMixedCurrencies);

        var reconciliation = header.Mapping.Has(CsvColumnRole.Balance)
            ? CsvReconciliationResult.Create(rows, currency)
            : CsvReconciliationResult.NotAvailable(rows.Sum(x => x.SignedAmount), currency);

        CollectWarnings(
            warnings,
            effectiveProfile,
            header,
            read.HasUnterminatedQuote,
            dateDetection.IsAmbiguous,
            decimalSeparatorIsAmbiguous,
            hasMixedCurrencies,
            dataRows,
            issues);

        return new CsvStatementFile
        {
            ProfileId = effectiveProfile.Id,
            ProfileDisplayName = effectiveProfile.DisplayName,
            WasAutoDetected = effectiveProfile.IsAutoDetect,
            Delimiter = delimiter,
            DecimalSeparator = decimalSeparator,
            DateFormat = dateDetection.PrimaryFormat,
            Encoding = encoding,
            HeaderLineNumber = header.LineNumber,
            PreambleLines = preambleLines,
            Mapping = header.Mapping,
            AmountConvention = convention,
            Rows = rows,
            Issues = issues,
            Warnings = warnings,
            AccountIdentifier = CsvAccountIdentifierScanner.FindIban(preambleLines),
            Currency = currency,
            Reconciliation = reconciliation
        };
    }

    /// <summary>
    /// Cheap content sniff, used to route an upload by content rather than by extension.
    ///
    /// <para>
    /// Conservative on purpose. MT940 markers veto the answer outright, because an MT940
    /// statement is full of colons and commas and would otherwise look tabular. What is left has
    /// to have a line that both splits into three or more fields on a plausible delimiter and
    /// resolves to a usable column mapping - "several commas on a line" alone would claim every
    /// text file the user ever uploads.
    /// </para>
    /// </summary>
    public static bool LooksLikeCsv(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // The byte-order mark is stripped first: it is not whitespace to .NET, so leaving it in
        // front of ":20:" would stop the MT940 veto from matching and offer a real MT940
        // statement to the CSV sniff.
        var head = (text.Length <= DelimiterDetectionLength
                ? text
                : text[..DelimiterDetectionLength])
            .TrimStart('\uFEFF');

        if (Mt940MarkerRegex.IsMatch(head))
        {
            return false;
        }

        foreach (var delimiter in CandidateDelimiters)
        {
            var rows = CsvReader.Read(head, delimiter).Rows;

            if (ScoreHeaderCandidates(rows, CsvStatementProfiles.Auto) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static char DetectDelimiter(string text, CsvStatementProfile profile)
    {
        var head = text.Length <= DelimiterDetectionLength
            ? text
            : text[..DelimiterDetectionLength];

        HeaderCandidate? best = null;
        var bestDelimiter = ';';

        foreach (var delimiter in CandidateDelimiters)
        {
            var rows = CsvReader.Read(head, delimiter, profile.Quote).Rows;

            var candidate = ScoreHeaderCandidates(rows, profile);

            if (candidate is null)
            {
                continue;
            }

            if (best is null || candidate.Score > best.Score)
            {
                best = candidate;
                bestDelimiter = delimiter;
            }
        }

        if (best is null)
        {
            throw new CsvParseException(
                "No column layout could be recognised in this file. It was checked as "
                + "semicolon-, comma-, tab- and pipe-separated, and none of them produced a "
                + "header row with a date column and an amount column. Check that the file is a "
                + "transaction export rather than a summary, or pick a CSV profile explicitly.");
        }

        return bestDelimiter;
    }

    private sealed record HeaderCandidate(
        int RowIndex,
        int LineNumber,
        CsvColumnMapping Mapping,
        int Score);

    /// <summary>
    /// Finds the header row.
    ///
    /// <para>
    /// By searching rather than by counting, because a preamble is not a fixed size: PostFinance
    /// puts several lines above the header, some banks put one, and any of them can grow a line
    /// when the bank adds a field. The header is simply the row within the first
    /// <see cref="CsvStatementProfile.HeaderSearchRowLimit"/> that resolves to the most column
    /// roles while still having a date column and an amount column.
    /// </para>
    /// </summary>
    private static HeaderCandidate FindHeader(
        IReadOnlyList<CsvRow> rows,
        CsvStatementProfile profile,
        char delimiter)
    {
        if (!profile.HasHeaderRow)
        {
            var skipped = profile.PreambleRowsToSkip ?? 0;

            var mapping = new CsvColumnMapping
            {
                ColumnIndexByRole = profile.ColumnIndexOverrides.ToDictionary(x => x.Key, x => x.Value),
                Headers = BuildSyntheticHeaders(rows, skipped)
            };

            if (!mapping.HasMinimumViableMapping)
            {
                throw new CsvParseException(
                    "This profile expects a file without a header row, but it does not say which "
                    + "column holds the date and which holds the amount.");
            }

            return new HeaderCandidate(skipped - 1, skipped, mapping, Score: 0);
        }

        if (profile.PreambleRowsToSkip is { } fixedSkip)
        {
            if (fixedSkip >= rows.Count)
            {
                throw new CsvParseException(
                    $"The profile skips {fixedSkip} rows before the header, but the file only has "
                    + $"{rows.Count}.");
            }

            var mapping = CsvColumnMapper.Resolve(rows[fixedSkip].Fields, profile);

            if (!mapping.HasMinimumViableMapping)
            {
                throw new CsvParseException(
                    $"Row {rows[fixedSkip].LineNumber} was expected to be the header row, but no "
                    + "date column and amount column could be recognised in it: "
                    + $"{DescribeHeaders(rows[fixedSkip].Fields)}.");
            }

            return new HeaderCandidate(fixedSkip, rows[fixedSkip].LineNumber, mapping, Score: 0);
        }

        return ScoreHeaderCandidates(rows, profile)
            ?? throw new CsvParseException(BuildNoHeaderMessage(rows, profile, delimiter));
    }

    private static HeaderCandidate? ScoreHeaderCandidates(
        IReadOnlyList<CsvRow> rows,
        CsvStatementProfile profile)
    {
        HeaderCandidate? best = null;

        var limit = Math.Min(rows.Count, Math.Max(1, profile.HeaderSearchRowLimit));

        for (var index = 0; index < limit; index++)
        {
            var row = rows[index];

            if (row.IsEmpty || row.Fields.Count < 2)
            {
                continue;
            }

            var mapping = CsvColumnMapper.Resolve(row.Fields, profile);

            if (!mapping.HasMinimumViableMapping)
            {
                continue;
            }

            // A header is worth more the more roles it explains, and a row followed by rows of
            // the same width is more likely to be a header than a stray label line that happens
            // to contain the word "Datum".
            var score = (mapping.ColumnIndexByRole.Count * 10)
                + (index + 1 < rows.Count && rows[index + 1].Fields.Count == row.Fields.Count ? 5 : 0);

            if (best is null || score > best.Score)
            {
                best = new HeaderCandidate(index, row.LineNumber, mapping, score);
            }
        }

        return best;
    }

    private static IReadOnlyList<string> BuildSyntheticHeaders(
        IReadOnlyList<CsvRow> rows,
        int skipped)
    {
        var width = rows.Count > skipped ? rows[skipped].Fields.Count : 0;

        return Enumerable
            .Range(1, width)
            .Select(x => $"#{x}")
            .ToList();
    }

    private static string BuildNoHeaderMessage(
        IReadOnlyList<CsvRow> rows,
        CsvStatementProfile profile,
        char delimiter)
    {
        var inspected = Math.Min(rows.Count, Math.Max(1, profile.HeaderSearchRowLimit));

        var firstRowHeaders = rows.Count > 0
            ? DescribeHeaders(rows[0].Fields)
            : "(none)";

        return $"No header row was found in the first {inspected} rows when reading the file with "
            + $"'{DescribeDelimiter(delimiter)}' as the separator. A header row must contain a date "
            + "column (Datum, Buchungsdatum, Valuta, Date, ...) and either an amount column "
            + "(Betrag, Amount, Montant) or a debit and a credit column (Belastung/Gutschrift). "
            + $"The first row read was: {firstRowHeaders}.";
    }

    private static string DescribeHeaders(IReadOnlyList<string> fields)
    {
        return string.Join(
            " | ",
            fields.Take(12).Select(x => x.Trim()));
    }

    public static string DescribeDelimiter(char delimiter)
    {
        return delimiter switch
        {
            '\t' => "Tab",
            ' ' => "Space",
            _ => delimiter.ToString()
        };
    }

    private static CsvAmountConvention ResolveAmountConvention(
        CsvStatementProfile profile,
        CsvColumnMapping mapping)
    {
        if (profile.AmountConvention != CsvAmountConvention.Auto)
        {
            return profile.AmountConvention;
        }

        if (mapping.Has(CsvColumnRole.Debit) || mapping.Has(CsvColumnRole.Credit))
        {
            // Two columns beat one: a file with Betrag *and* Belastung/Gutschrift is one where
            // Betrag is usually the gross or the foreign-currency amount, and the pair is the
            // booked one.
            return CsvAmountConvention.SeparateDebitCredit;
        }

        if (mapping.Has(CsvColumnRole.Amount) && mapping.Has(CsvColumnRole.DebitCreditIndicator))
        {
            return CsvAmountConvention.AmountWithIndicator;
        }

        return CsvAmountConvention.SignedAmount;
    }

    private static CsvDecimalSeparator ResolveDecimalSeparator(
        CsvStatementProfile profile,
        CsvColumnMapping mapping,
        IReadOnlyList<CsvRow> dataRows,
        out bool isAmbiguous)
    {
        if (profile.DecimalSeparator != CsvDecimalSeparator.Auto)
        {
            isAmbiguous = false;
            return profile.DecimalSeparator;
        }

        var detection = CsvAmountParser.Detect(
            CollectCells(
                dataRows,
                mapping,
                [
                    CsvColumnRole.Amount,
                    CsvColumnRole.Debit,
                    CsvColumnRole.Credit,
                    CsvColumnRole.Balance
                ]));

        isAmbiguous = detection.IsAmbiguous;

        return detection.Separator;
    }

    private static CsvDateParser.Detection ResolveDateFormats(
        CsvStatementProfile profile,
        CsvColumnMapping mapping,
        IReadOnlyList<CsvRow> dataRows)
    {
        var samples = CollectCells(
                dataRows,
                mapping,
                [CsvColumnRole.BookingDate, CsvColumnRole.ValueDate])
            .ToList();

        var detection = CsvDateParser.Detect(samples, profile.DateFormats);

        if (detection.Succeeded || samples.Count == 0)
        {
            return detection;
        }

        var examples = string.Join(
            ", ",
            samples.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(3));

        throw new CsvParseException(
            "The dates in this file could not be read. No single date format explains every "
            + $"value in the date column - for example: {examples}. Formats such as 15.01.2026, "
            + "2026-01-15 and 15/01/2026 are recognised; a mixture of them in one column is not.");
    }

    private static IEnumerable<string> CollectCells(
        IReadOnlyList<CsvRow> rows,
        CsvColumnMapping mapping,
        IReadOnlyList<CsvColumnRole> roles)
    {
        var indexes = roles
            .Select(mapping.IndexOf)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToList();

        foreach (var row in rows)
        {
            foreach (var index in indexes)
            {
                var value = row.Field(index);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }
    }

    private static IReadOnlyList<CsvStatementRow> ParseRows(
        IReadOnlyList<CsvRow> dataRows,
        HeaderCandidate header,
        CsvStatementProfile profile,
        CsvAmountConvention convention,
        CsvDecimalSeparator decimalSeparator,
        IReadOnlyList<string> dateFormats,
        List<CsvRowIssue> issues)
    {
        var mapping = header.Mapping;
        var negateDebit = ShouldNegateDebit(dataRows, mapping, profile, decimalSeparator);

        var rows = new List<CsvStatementRow>(dataRows.Count);

        foreach (var row in dataRows)
        {
            var parsed = ParseRow(
                row,
                mapping,
                profile,
                convention,
                decimalSeparator,
                dateFormats,
                negateDebit,
                rows.Count,
                issues);

            if (parsed is not null)
            {
                rows.Add(parsed);
            }
        }

        return rows;
    }

    /// <summary>
    /// Whether a debit column has to be negated.
    ///
    /// <para>
    /// A column headed <c>Belastung</c> normally holds an unsigned amount, and negating it is
    /// what makes an expense an expense. But a handful of exports sign it as well, and negating
    /// an already-negative number turns a 2'400 franc rent payment into 2'400 francs of income -
    /// which the plan then projects forward for thirty years. So the values get the last word
    /// even over a profile that says the column is unsigned: if every debit in the file is
    /// already negative, the column is left alone.
    /// </para>
    /// </summary>
    private static bool ShouldNegateDebit(
        IReadOnlyList<CsvRow> dataRows,
        CsvColumnMapping mapping,
        CsvStatementProfile profile,
        CsvDecimalSeparator decimalSeparator)
    {
        if (!profile.DebitColumnIsUnsigned)
        {
            return false;
        }

        var debitIndex = mapping.IndexOf(CsvColumnRole.Debit);

        if (debitIndex is null)
        {
            return true;
        }

        var values = new List<decimal>();

        foreach (var row in dataRows)
        {
            if (CsvAmountParser.TryParse(row.Field(debitIndex.Value), decimalSeparator, out var value))
            {
                values.Add(value);
            }
        }

        if (values.Count == 0)
        {
            return true;
        }

        return !values.All(x => x <= 0m) || values.All(x => x == 0m);
    }

    private static CsvStatementRow? ParseRow(
        CsvRow row,
        CsvColumnMapping mapping,
        CsvStatementProfile profile,
        CsvAmountConvention convention,
        CsvDecimalSeparator decimalSeparator,
        IReadOnlyList<string> dateFormats,
        bool negateDebit,
        int rowIndex,
        List<CsvRowIssue> issues)
    {
        var bookingRaw = ReadCell(row, mapping, CsvColumnRole.BookingDate);
        var valueRaw = ReadCell(row, mapping, CsvColumnRole.ValueDate);
        var amountRaw = ReadCell(row, mapping, CsvColumnRole.Amount);
        var debitRaw = ReadCell(row, mapping, CsvColumnRole.Debit);
        var creditRaw = ReadCell(row, mapping, CsvColumnRole.Credit);

        var hasAnyDate = !string.IsNullOrWhiteSpace(bookingRaw) || !string.IsNullOrWhiteSpace(valueRaw);

        var hasAnyAmount = !string.IsNullOrWhiteSpace(amountRaw)
            || !string.IsNullOrWhiteSpace(debitRaw)
            || !string.IsNullOrWhiteSpace(creditRaw);

        if (!hasAnyDate && !hasAnyAmount)
        {
            // Neither a date nor an amount: a section label, a disclaimer or a blank separator
            // line. Reporting these as failures would bury the rows that genuinely failed under
            // noise the user cannot act on.
            return null;
        }

        if (!hasAnyDate)
        {
            issues.Add(CreateIssue(row, CsvRowIssueKind.MissingDate, mapping, CsvColumnRole.BookingDate, null));
            return null;
        }

        DateOnly? bookingDate = null;
        DateOnly? valueDate = null;

        if (!string.IsNullOrWhiteSpace(bookingRaw))
        {
            if (CsvDateParser.TryParse(bookingRaw, dateFormats, out var parsedBooking))
            {
                bookingDate = parsedBooking;
            }
            else
            {
                issues.Add(CreateIssue(row, CsvRowIssueKind.UnreadableDate, mapping, CsvColumnRole.BookingDate, bookingRaw));
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(valueRaw))
        {
            if (CsvDateParser.TryParse(valueRaw, dateFormats, out var parsedValue))
            {
                valueDate = parsedValue;
            }
            else
            {
                issues.Add(CreateIssue(row, CsvRowIssueKind.UnreadableDate, mapping, CsvColumnRole.ValueDate, valueRaw));
                return null;
            }
        }

        if (!TryReadSignedAmount(
                row,
                mapping,
                profile,
                convention,
                decimalSeparator,
                negateDebit,
                amountRaw,
                debitRaw,
                creditRaw,
                issues,
                out var signedAmount))
        {
            return null;
        }

        decimal? balance = null;

        var balanceRaw = ReadCell(row, mapping, CsvColumnRole.Balance);

        if (!string.IsNullOrWhiteSpace(balanceRaw))
        {
            if (CsvAmountParser.TryParse(balanceRaw, decimalSeparator, out var parsedBalance))
            {
                balance = parsedBalance;
            }
            else
            {
                // The transaction is still good; only the reconciliation check is lost.
                issues.Add(CreateIssue(row, CsvRowIssueKind.UnreadableBalance, mapping, CsvColumnRole.Balance, balanceRaw));
            }
        }

        return new CsvStatementRow
        {
            LineNumber = row.LineNumber,
            RowIndex = rowIndex,
            BookingDate = bookingDate,
            ValueDate = valueDate,
            SignedAmount = signedAmount,
            Currency = ReadCurrency(row, mapping, profile),
            Description = NullIfBlank(ReadCell(row, mapping, CsvColumnRole.Description)),
            Counterparty = NullIfBlank(ReadCell(row, mapping, CsvColumnRole.Counterparty)),
            Reference = NullIfBlank(ReadCell(row, mapping, CsvColumnRole.Reference)),
            Balance = balance,
            RawText = row.RawText
        };
    }

    private static bool TryReadSignedAmount(
        CsvRow row,
        CsvColumnMapping mapping,
        CsvStatementProfile profile,
        CsvAmountConvention convention,
        CsvDecimalSeparator decimalSeparator,
        bool negateDebit,
        string amountRaw,
        string debitRaw,
        string creditRaw,
        List<CsvRowIssue> issues,
        out decimal signedAmount)
    {
        signedAmount = 0m;

        switch (convention)
        {
            case CsvAmountConvention.SeparateDebitCredit:
            {
                var hasDebit = !string.IsNullOrWhiteSpace(debitRaw);
                var hasCredit = !string.IsNullOrWhiteSpace(creditRaw);

                if (hasDebit && hasCredit)
                {
                    // Genuinely contradictory. Guessing which one the bank meant is how a
                    // transaction ends up in the plan at the wrong sign.
                    issues.Add(CreateIssue(row, CsvRowIssueKind.BothDebitAndCredit, mapping, CsvColumnRole.Debit, $"{debitRaw} / {creditRaw}"));
                    return false;
                }

                if (!hasDebit && !hasCredit)
                {
                    // Some exports carry the pair *and* a signed amount column; fall back to it
                    // before giving up.
                    if (!string.IsNullOrWhiteSpace(amountRaw))
                    {
                        return TryReadSingleAmount(row, mapping, decimalSeparator, amountRaw, issues, out signedAmount);
                    }

                    issues.Add(CreateIssue(row, CsvRowIssueKind.MissingAmount, mapping, CsvColumnRole.Debit, null));
                    return false;
                }

                var raw = hasDebit ? debitRaw : creditRaw;
                var role = hasDebit ? CsvColumnRole.Debit : CsvColumnRole.Credit;

                if (!CsvAmountParser.TryParse(raw, decimalSeparator, out var value))
                {
                    issues.Add(CreateIssue(row, CsvRowIssueKind.UnreadableAmount, mapping, role, raw));
                    return false;
                }

                signedAmount = hasDebit
                    ? (negateDebit ? -Math.Abs(value) : value)
                    : Math.Abs(value);

                return true;
            }

            case CsvAmountConvention.AmountWithIndicator:
            {
                if (!TryReadSingleAmount(row, mapping, decimalSeparator, amountRaw, issues, out var value))
                {
                    return false;
                }

                var indicatorRaw = ReadCell(row, mapping, CsvColumnRole.DebitCreditIndicator);
                var indicator = indicatorRaw.Trim().ToLowerInvariant();

                if (profile.DebitIndicators.Contains(indicator, StringComparer.OrdinalIgnoreCase))
                {
                    signedAmount = -Math.Abs(value);
                    return true;
                }

                if (profile.CreditIndicators.Contains(indicator, StringComparer.OrdinalIgnoreCase))
                {
                    signedAmount = Math.Abs(value);
                    return true;
                }

                if (indicator.Length == 0)
                {
                    // No indicator: the amount's own sign is the only information there is.
                    signedAmount = value;
                    return true;
                }

                issues.Add(CreateIssue(row, CsvRowIssueKind.UnreadableDebitCreditIndicator, mapping, CsvColumnRole.DebitCreditIndicator, indicatorRaw));
                return false;
            }

            default:
                return TryReadSingleAmount(row, mapping, decimalSeparator, amountRaw, issues, out signedAmount);
        }
    }

    private static bool TryReadSingleAmount(
        CsvRow row,
        CsvColumnMapping mapping,
        CsvDecimalSeparator decimalSeparator,
        string amountRaw,
        List<CsvRowIssue> issues,
        out decimal signedAmount)
    {
        signedAmount = 0m;

        if (string.IsNullOrWhiteSpace(amountRaw))
        {
            issues.Add(CreateIssue(row, CsvRowIssueKind.MissingAmount, mapping, CsvColumnRole.Amount, null));
            return false;
        }

        if (!CsvAmountParser.TryParse(amountRaw, decimalSeparator, out signedAmount))
        {
            issues.Add(CreateIssue(row, CsvRowIssueKind.UnreadableAmount, mapping, CsvColumnRole.Amount, amountRaw));
            return false;
        }

        return true;
    }

    private static CsvRowIssue CreateIssue(
        CsvRow row,
        CsvRowIssueKind kind,
        CsvColumnMapping mapping,
        CsvColumnRole role,
        string? value)
    {
        return new CsvRowIssue
        {
            LineNumber = row.LineNumber,
            Kind = kind,
            RawText = row.RawText,
            ColumnHeader = mapping.HeaderOf(role),
            Value = value
        };
    }

    private static string ReadCell(CsvRow row, CsvColumnMapping mapping, CsvColumnRole role)
    {
        var index = mapping.IndexOf(role);

        return index is null
            ? string.Empty
            : row.Field(index.Value).Trim();
    }

    private static string ReadCurrency(
        CsvRow row,
        CsvColumnMapping mapping,
        CsvStatementProfile profile)
    {
        var raw = ReadCell(row, mapping, CsvColumnRole.Currency).ToUpperInvariant();

        return raw.Length == 3 && raw.All(char.IsAsciiLetterUpper)
            ? raw
            : profile.DefaultCurrency;
    }

    private static string ResolveFileCurrency(
        IReadOnlyList<CsvStatementRow> rows,
        IReadOnlyList<string> preambleLines,
        CsvStatementProfile profile,
        out bool hasMixedCurrencies)
    {
        var distinct = rows
            .Select(x => x.Currency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        hasMixedCurrencies = distinct.Count > 1;

        if (distinct.Count == 1)
        {
            return distinct[0];
        }

        if (distinct.Count > 1)
        {
            // The batch has to name one currency. The most common is the account's; the rows
            // keep their own, so nothing is lost.
            return rows
                .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key, StringComparer.Ordinal)
                .First()
                .Key;
        }

        return CsvAccountIdentifierScanner.FindCurrency(preambleLines)
            ?? profile.DefaultCurrency;
    }

    private static void CollectWarnings(
        List<CsvParseWarning> warnings,
        CsvStatementProfile profile,
        HeaderCandidate header,
        bool hasUnterminatedQuote,
        bool dateFormatIsAmbiguous,
        bool decimalSeparatorIsAmbiguous,
        bool hasMixedCurrencies,
        IReadOnlyList<CsvRow> dataRows,
        IReadOnlyList<CsvRowIssue> issues)
    {
        if (profile.IsAutoDetect)
        {
            warnings.Add(CsvParseWarning.FormatWasAutoDetected);
        }

        if (dateFormatIsAmbiguous)
        {
            warnings.Add(CsvParseWarning.AmbiguousDateFormat);
        }

        if (decimalSeparatorIsAmbiguous)
        {
            warnings.Add(CsvParseWarning.AmbiguousDecimalSeparator);
        }

        if (hasUnterminatedQuote)
        {
            warnings.Add(CsvParseWarning.UnterminatedQuote);
        }

        if (!header.Mapping.Has(CsvColumnRole.ValueDate))
        {
            warnings.Add(CsvParseWarning.NoValueDateColumn);
        }

        if (!header.Mapping.Has(CsvColumnRole.Balance))
        {
            warnings.Add(CsvParseWarning.NoBalanceColumn);
        }

        if (hasMixedCurrencies)
        {
            warnings.Add(CsvParseWarning.MixedCurrencies);
        }

        var headerWidth = header.Mapping.Headers.Count;

        if (dataRows.Any(row => row.Fields.Count != headerWidth))
        {
            warnings.Add(CsvParseWarning.InconsistentColumnCount);
        }

        if (issues.Count > 0)
        {
            warnings.Add(CsvParseWarning.SomeRowsCouldNotBeRead);
        }
    }

    private static string? NullIfBlank(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
