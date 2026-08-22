using System.Reflection;

namespace CashFlowPlanner.Core.Tests.Banking.Csv;

/// <summary>
/// Loads the embedded CSV fixtures.
///
/// <para>
/// The fixtures are written the way Swiss exports actually are, not the way the parser would
/// find convenient: semicolons, apostrophe grouping, CRLF line endings, umlauts in Latin-1, a
/// byte-order mark, quoted fields containing the delimiter and a quoted field containing a
/// newline. Anything that is only ever tested against a string built in the test method is
/// tested against a shape the parser author already agreed with.
/// </para>
///
/// <para>
/// Bytes, never text: two of these files are not UTF-8, and reading them through a decoder
/// before handing them to the parser would test a decode the production path never performs.
/// </para>
/// </summary>
internal static class CsvFixture
{
    public const string SwissApostrophe = "SwissApostrophe.csv";
    public const string GermanFormat = "GermanFormat.csv";
    public const string DebitCreditColumns = "DebitCreditColumns.csv";
    public const string WithPreamble = "WithPreamble.csv";
    public const string Latin1 = "Latin1.csv";
    public const string IsoComma = "IsoComma.csv";
    public const string Malformed = "Malformed.csv";
    public const string MalformedDates = "MalformedDates.csv";
    public const string SlashDatesDayFirst = "SlashDatesDayFirst.csv";
    public const string SlashDatesMonthFirst = "SlashDatesMonthFirst.csv";
    public const string SlashDatesAmbiguous = "SlashDatesAmbiguous.csv";
    public const string UnreadableRows = "UnreadableRows.csv";
    public const string TabSeparated = "TabSeparated.csv";
    public const string AmountWithIndicator = "AmountWithIndicator.csv";
    public const string Utf8Bom = "Utf8Bom.csv";

    private const string ResourcePrefix = "CashFlowPlanner.Core.Tests.Banking.Csv.Fixtures.";

    public static byte[] ReadBytes(string fixtureName)
    {
        var assembly = typeof(CsvFixture).GetTypeInfo().Assembly;

        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + fixtureName)
            ?? throw new InvalidOperationException(
                $"Embedded fixture '{fixtureName}' not found. Available: "
                + string.Join(", ", assembly.GetManifestResourceNames()));

        using var memoryStream = new MemoryStream();

        stream.CopyTo(memoryStream);

        return memoryStream.ToArray();
    }
}
