using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CashFlowPlanner.Core.Banking.Mt940;

public sealed class Mt940Parser
{
    private static readonly Regex BalanceRegex = new(
        @"^(?<indicator>RC|RD|C|D)(?<date>\d{6})(?<currency>[A-Z]{3})(?<amount>[0-9,]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TransactionFirstLineRegex = new(
        @"^(?<valueDate>\d{6})(?<bookingDate>\d{4})?(?<indicator>RC|RD|C|D)(?<amount>[0-9,]+)(?<transactionCode>[A-Z][A-Z0-9]{3})(?<details>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TagStartRegex = new(
        @"^:(?<tag>[0-9A-Z]{2,4}[A-Z]?):(?<content>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Mt940Statement Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var text = Decode(bytes);

        return Parse(text);
    }

    public Mt940Statement Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new Mt940ParseException("The MT940 content is empty.");
        }

        var body = ExtractBody(text);
        var tags = ParseTags(body);

        var transactionReference = GetSingleTagValue(tags, "20");
        var accountIdentifier = GetSingleTagValue(tags, "25");
        var statementNumber = GetSingleTagValue(tags, "28C");

        var openingBalance = ParseOptionalBalance(
            GetSingleTagValue(tags, "60F"));

        var closingBalance = ParseOptionalBalance(
            GetSingleTagValue(tags, "62F"));

        var transactionCurrency =
            openingBalance?.Currency
            ?? closingBalance?.Currency
            ?? "CHF";

        var transactions = ParseTransactions(
            tags,
            transactionCurrency);

        var reconciliation = Mt940ReconciliationResult.Create(
            openingBalance,
            closingBalance,
            transactions);

        return new Mt940Statement
        {
            TransactionReference = transactionReference,
            AccountIdentifier = accountIdentifier,
            StatementNumber = statementNumber,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            Transactions = transactions,
            Reconciliation = reconciliation,
            RawBody = body
        };
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var utf8Encoding = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        try
        {
            return utf8Encoding.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // UBS MT940 exports may contain Western European characters.
            // Latin1 safely decodes byte values such as ä/ö/ü when files are not UTF-8.
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static string ExtractBody(string text)
    {
        var normalizedText = NormalizeLineEndings(text);

        var block4Start = normalizedText.IndexOf(
            "{4:",
            StringComparison.Ordinal);

        if (block4Start < 0)
        {
            return normalizedText.Trim();
        }

        var contentStart = block4Start + "{4:".Length;

        var block4End = normalizedText.IndexOf(
            "\n-}",
            contentStart,
            StringComparison.Ordinal);

        if (block4End < 0)
        {
            block4End = normalizedText.IndexOf(
                "-}",
                contentStart,
                StringComparison.Ordinal);
        }

        if (block4End < 0)
        {
            return normalizedText.Trim();
        }

        return normalizedText[contentStart..block4End].Trim();
    }

    private static string NormalizeLineEndings(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static IReadOnlyList<Mt940Tag> ParseTags(string body)
    {
        var tags = new List<Mt940Tag>();
        Mt940TagBuilder? currentTag = null;

        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var tagMatch = TagStartRegex.Match(line);

            if (tagMatch.Success)
            {
                if (currentTag is not null)
                {
                    tags.Add(currentTag.Build());
                }

                currentTag = new Mt940TagBuilder(
                    tagMatch.Groups["tag"].Value,
                    tagMatch.Groups["content"].Value);

                continue;
            }

            if (currentTag is null)
            {
                continue;
            }

            currentTag.AppendLine(line);
        }

        if (currentTag is not null)
        {
            tags.Add(currentTag.Build());
        }

        return tags;
    }

    private static string? GetSingleTagValue(
        IReadOnlyList<Mt940Tag> tags,
        string tagName)
    {
        return tags
            .Where(x => string.Equals(
                x.Name,
                tagName,
                StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Content.Trim())
            .LastOrDefault();
    }

    private static Mt940Balance? ParseOptionalBalance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var firstLine = value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        var match = BalanceRegex.Match(firstLine);

        if (!match.Success)
        {
            throw new Mt940ParseException(
                $"Could not parse MT940 balance line '{firstLine}'.");
        }

        var indicator = ParseDebitCreditIndicator(
            match.Groups["indicator"].Value);

        var amount = ParseSignedAmount(
            match.Groups["amount"].Value,
            indicator);

        return new Mt940Balance
        {
            Date = ParseMt940Date(match.Groups["date"].Value),
            Amount = amount,
            Currency = match.Groups["currency"].Value,
            DebitCreditIndicator = indicator
        };
    }

    private static IReadOnlyList<Mt940Transaction> ParseTransactions(
        IReadOnlyList<Mt940Tag> tags,
        string currency)
    {
        var transactions = new List<Mt940Transaction>();

        for (var index = 0; index < tags.Count; index++)
        {
            var tag = tags[index];

            if (!string.Equals(
                    tag.Name,
                    "61",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? raw86 = null;

            if (index + 1 < tags.Count &&
                string.Equals(
                    tags[index + 1].Name,
                    "86",
                    StringComparison.OrdinalIgnoreCase))
            {
                raw86 = tags[index + 1].Content.Trim();
            }

            transactions.Add(ParseTransaction(
                tag.Content,
                raw86,
                currency));
        }

        return transactions;
    }

    private static Mt940Transaction ParseTransaction(
        string raw61Content,
        string? raw86,
        string currency)
    {
        var raw61 = raw61Content.Trim();

        var lines = raw61
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToList();

        if (lines.Count == 0)
        {
            throw new Mt940ParseException("Encountered an empty MT940 :61: transaction.");
        }

        var firstLine = lines[0];
        var continuationText = string.Join(
            " ",
            lines.Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)));

        var match = TransactionFirstLineRegex.Match(firstLine);

        if (!match.Success)
        {
            throw new Mt940ParseException(
                $"Could not parse MT940 transaction line '{firstLine}'.");
        }

        var valueDate = ParseMt940Date(
            match.Groups["valueDate"].Value);

        var bookingDate = ParseOptionalBookingDate(
            valueDate,
            match.Groups["bookingDate"].Value);

        var indicator = ParseDebitCreditIndicator(
            match.Groups["indicator"].Value);

        var signedAmount = ParseSignedAmount(
            match.Groups["amount"].Value,
            indicator);

        var details = match.Groups["details"].Value;

        var referenceParts = ParseReferenceParts(details);

        var structured86 = ParseStructured86(raw86);

        var description = BuildDescription(
            continuationText,
            structured86.Text,
            raw86);

        return new Mt940Transaction
        {
            ValueDate = valueDate,
            BookingDate = bookingDate,
            DebitCreditIndicator = indicator,
            SignedAmount = signedAmount,
            Currency = currency,
            TransactionCode = match.Groups["transactionCode"].Value,
            CustomerReference = referenceParts.CustomerReference,
            BankReference = referenceParts.BankReference,
            SupplementaryDetails = referenceParts.SupplementaryDetails,
            Structured86Code = structured86.Code,
            Structured86Text = structured86.Text,
            Description = description,
            Raw61 = raw61,
            Raw86 = raw86
        };
    }

    private static Mt940ReferenceParts ParseReferenceParts(string details)
    {
        var trimmedDetails = details.Trim();

        if (string.IsNullOrWhiteSpace(trimmedDetails))
        {
            return new Mt940ReferenceParts();
        }

        var separatorIndex = trimmedDetails.IndexOf(
            "//",
            StringComparison.Ordinal);

        if (separatorIndex < 0)
        {
            return new Mt940ReferenceParts
            {
                CustomerReference = NullIfWhiteSpace(trimmedDetails)
            };
        }

        var customerReference = trimmedDetails[..separatorIndex];
        var afterSeparator = trimmedDetails[(separatorIndex + 2)..];

        string? bankReference;
        string? supplementaryDetails = null;

        var whitespaceIndex = afterSeparator.IndexOfAny([' ', '\t']);

        if (whitespaceIndex >= 0)
        {
            bankReference = afterSeparator[..whitespaceIndex];
            supplementaryDetails = afterSeparator[(whitespaceIndex + 1)..];
        }
        else
        {
            bankReference = afterSeparator;
        }

        return new Mt940ReferenceParts
        {
            CustomerReference = NullIfWhiteSpace(customerReference),
            BankReference = NullIfWhiteSpace(bankReference),
            SupplementaryDetails = NullIfWhiteSpace(supplementaryDetails)
        };
    }

    private static Mt940Structured86 ParseStructured86(string? raw86)
    {
        if (string.IsNullOrWhiteSpace(raw86))
        {
            return new Mt940Structured86();
        }

        var trimmed = raw86.Trim();

        var separatorIndex = trimmed.IndexOf(
            '?',
            StringComparison.Ordinal);

        if (separatorIndex <= 0)
        {
            return new Mt940Structured86
            {
                Text = NormalizeText(trimmed)
            };
        }

        var code = trimmed[..separatorIndex].Trim();
        var text = trimmed[(separatorIndex + 1)..];

        return new Mt940Structured86
        {
            Code = NullIfWhiteSpace(code),
            Text = NormalizeText(text)
        };
    }

    private static string BuildDescription(
        string continuationText,
        string? structured86Text,
        string? raw86)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(continuationText))
        {
            parts.Add(continuationText.Trim());
        }

        if (!string.IsNullOrWhiteSpace(structured86Text))
        {
            parts.Add(structured86Text.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(raw86))
        {
            parts.Add(raw86.Trim());
        }

        return NormalizeText(string.Join(" ", parts));
    }

    private static DateOnly ParseMt940Date(string value)
    {
        if (value.Length != 6)
        {
            throw new Mt940ParseException(
                $"Invalid MT940 date '{value}'. Expected YYMMDD.");
        }

        var year = int.Parse(value[..2], CultureInfo.InvariantCulture);
        var month = int.Parse(value.Substring(2, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(value.Substring(4, 2), CultureInfo.InvariantCulture);

        year += year >= 70
            ? 1900
            : 2000;

        return new DateOnly(year, month, day);
    }

    private static DateOnly? ParseOptionalBookingDate(
        DateOnly valueDate,
        string bookingDateValue)
    {
        if (string.IsNullOrWhiteSpace(bookingDateValue))
        {
            return null;
        }

        if (bookingDateValue.Length != 4)
        {
            throw new Mt940ParseException(
                $"Invalid MT940 booking date '{bookingDateValue}'. Expected MMDD.");
        }

        var month = int.Parse(bookingDateValue[..2], CultureInfo.InvariantCulture);
        var day = int.Parse(bookingDateValue.Substring(2, 2), CultureInfo.InvariantCulture);

        var bookingDate = new DateOnly(
            valueDate.Year,
            month,
            day);

        if (bookingDate > valueDate.AddMonths(6))
        {
            bookingDate = bookingDate.AddYears(-1);
        }
        else if (bookingDate < valueDate.AddMonths(-6))
        {
            bookingDate = bookingDate.AddYears(1);
        }

        return bookingDate;
    }

    private static Mt940DebitCreditIndicator ParseDebitCreditIndicator(string value)
    {
        return value switch
        {
            "C" => Mt940DebitCreditIndicator.Credit,
            "D" => Mt940DebitCreditIndicator.Debit,
            "RC" => Mt940DebitCreditIndicator.ReversalOfCredit,
            "RD" => Mt940DebitCreditIndicator.ReversalOfDebit,
            _ => throw new Mt940ParseException(
                $"Unsupported MT940 debit/credit indicator '{value}'.")
        };
    }

    private static decimal ParseSignedAmount(
        string amountValue,
        Mt940DebitCreditIndicator indicator)
    {
        var unsignedAmount = decimal.Parse(
            amountValue.Replace(',', '.'),
            CultureInfo.InvariantCulture);

        return indicator switch
        {
            Mt940DebitCreditIndicator.Credit => unsignedAmount,
            Mt940DebitCreditIndicator.Debit => -unsignedAmount,
            Mt940DebitCreditIndicator.ReversalOfCredit => -unsignedAmount,
            Mt940DebitCreditIndicator.ReversalOfDebit => unsignedAmount,
            _ => throw new Mt940ParseException(
                $"Unsupported MT940 debit/credit indicator '{indicator}'.")
        };
    }

    private static string NormalizeText(string value)
    {
        return Regex.Replace(
                value.Trim(),
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed class Mt940TagBuilder
    {
        private readonly List<string> _lines = [];

        public Mt940TagBuilder(
            string name,
            string firstLine)
        {
            Name = name;
            _lines.Add(firstLine);
        }

        public string Name { get; }

        public void AppendLine(string line)
        {
            _lines.Add(line);
        }

        public Mt940Tag Build()
        {
            return new Mt940Tag(
                Name,
                string.Join('\n', _lines));
        }
    }

    private sealed record Mt940Tag(
        string Name,
        string Content);

    private sealed class Mt940ReferenceParts
    {
        public string? CustomerReference { get; init; }

        public string? BankReference { get; init; }

        public string? SupplementaryDetails { get; init; }
    }

    private sealed class Mt940Structured86
    {
        public string? Code { get; init; }

        public string? Text { get; init; }
    }
}