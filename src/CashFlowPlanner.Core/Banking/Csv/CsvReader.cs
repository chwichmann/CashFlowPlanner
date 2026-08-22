using System.Text;

namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// A hand-rolled RFC 4180 reader.
///
/// <para>
/// Hand-rolled because the project takes no NuGet dependency, and because the interesting part
/// of a bank CSV is not the tokenising anyway. What it must get right is small and specific:
/// a delimiter inside a quoted field (Swiss payment purposes contain semicolons), a newline
/// inside a quoted field (multi-line remittance information), and a doubled quote as an
/// escaped quote. <c>text.Split(';')</c> gets all three wrong, and gets them wrong by
/// splitting one transaction into two.
/// </para>
///
/// <para>
/// Deliberately tolerant beyond the RFC in two places, because bank exports are: a quote that
/// opens and never closes ends the record at end-of-file rather than throwing, and is
/// reported through <see cref="CsvReadResult.HasUnterminatedQuote"/>; and text appearing after
/// a closing quote ("abc"def) is appended rather than rejected. A malformed file should tell
/// the user which line is wrong, not refuse the other 300 rows.
/// </para>
/// </summary>
public static class CsvReader
{
    public sealed record CsvReadResult(
        IReadOnlyList<CsvRow> Rows,
        bool HasUnterminatedQuote);

    public static CsvReadResult Read(string text, char delimiter, char quote = '"')
    {
        ArgumentNullException.ThrowIfNull(text);

        var rows = new List<CsvRow>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var raw = new StringBuilder();

        var lineNumber = 1;
        var rowStartLine = 1;
        var inQuotes = false;
        var hasContent = false;
        var unterminatedQuote = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];

            if (inQuotes)
            {
                raw.Append(current);

                if (current == quote)
                {
                    // A doubled quote is one literal quote; a single one closes the field.
                    if (index + 1 < text.Length && text[index + 1] == quote)
                    {
                        field.Append(quote);
                        raw.Append(quote);
                        index++;
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                if (current == '\n')
                {
                    lineNumber++;
                }

                field.Append(current);
                continue;
            }

            if (current == quote && field.Length == 0)
            {
                inQuotes = true;
                hasContent = true;
                raw.Append(current);
                continue;
            }

            if (current == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
                raw.Append(current);
                hasContent = true;
                continue;
            }

            if (current is '\r' or '\n')
            {
                // \r\n, \n and a lone \r all end the record. Bank exports use all three, and a
                // file that has travelled through Windows, a Mac and a mail gateway may use
                // more than one of them.
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                lineNumber++;

                AddRow(rows, fields, field, raw, rowStartLine, hasContent);

                rowStartLine = lineNumber;
                hasContent = false;
                continue;
            }

            raw.Append(current);
            field.Append(current);
            hasContent = true;
        }

        if (inQuotes)
        {
            unterminatedQuote = true;
        }

        if (hasContent || field.Length > 0 || fields.Count > 0)
        {
            AddRow(rows, fields, field, raw, rowStartLine, hasContent: true);
        }

        return new CsvReadResult(rows, unterminatedQuote);
    }

    private static void AddRow(
        List<CsvRow> rows,
        List<string> fields,
        StringBuilder field,
        StringBuilder raw,
        int lineNumber,
        bool hasContent)
    {
        if (!hasContent && field.Length == 0 && fields.Count == 0)
        {
            // A blank line. Bank exports separate sections with them and end the file with one;
            // keeping them would push a phantom row into every count the preview shows.
            raw.Clear();
            return;
        }

        fields.Add(field.ToString());

        rows.Add(new CsvRow
        {
            LineNumber = lineNumber,
            Fields = fields.ToArray(),
            RawText = raw.ToString()
        });

        fields.Clear();
        field.Clear();
        raw.Clear();
    }
}
