using System.Text;

namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Turns the uploaded bytes into text, and says which encoding it used.
///
/// <para>
/// Unlike camt.053 there is no declaration to honour - a CSV file carries no statement of its
/// own encoding beyond an optional byte-order mark. So the order is: BOM if there is one,
/// then strict UTF-8, then Latin-1 as the fallback that cannot fail. Strict matters: decoding
/// Latin-1 bytes with a lenient UTF-8 decoder does not throw, it silently produces U+FFFD, and
/// the user gets "Z�rich Versicherung" in their transaction list with no indication that
/// anything went wrong.
/// </para>
///
/// <para>
/// Latin-1 is the right fallback rather than Windows-1252 for the accented characters that
/// actually occur in Swiss payee names (ä ö ü é è à ç); the two differ only in 0x80-0x9F,
/// which holds typographic quotes and the euro sign. Getting a curly quote wrong is cosmetic;
/// getting an umlaut wrong is what users notice.
/// </para>
/// </summary>
public static class CsvTextDecoder
{
    public sealed record Result(string Text, CsvTextEncoding Encoding, bool HadByteOrderMark);

    public static Result Decode(byte[] bytes, CsvTextEncoding preference = CsvTextEncoding.Auto)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
        {
            return new Result(string.Empty, CsvTextEncoding.Utf8, HadByteOrderMark: false);
        }

        if (StartsWithUtf8ByteOrderMark(bytes))
        {
            return new Result(
                DecodeUtf8Lenient(bytes, offset: 3),
                CsvTextEncoding.Utf8,
                HadByteOrderMark: true);
        }

        // UTF-16 is not a format any Swiss bank exports, but Excel's "Unicode Text (*.txt)"
        // save-as produces it, and a user who opened the bank's file in Excel and saved it
        // again arrives here. Decoding it costs three lines; failing on it looks like a bug in
        // the bank's export.
        if (StartsWithUtf16ByteOrderMark(bytes, out var utf16))
        {
            return new Result(
                utf16.GetString(bytes, 2, bytes.Length - 2),
                CsvTextEncoding.Utf8,
                HadByteOrderMark: true);
        }

        if (preference == CsvTextEncoding.Latin1)
        {
            return new Result(
                Encoding.Latin1.GetString(bytes),
                CsvTextEncoding.Latin1,
                HadByteOrderMark: false);
        }

        var strictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        try
        {
            return new Result(
                strictUtf8.GetString(bytes),
                CsvTextEncoding.Utf8,
                HadByteOrderMark: false);
        }
        catch (DecoderFallbackException)
        {
            // Not valid UTF-8. A profile that insisted on UTF-8 still gets text rather than an
            // exception - the encoding is reported alongside the preview, so a user seeing
            // mangled names knows which knob to turn.
            return new Result(
                Encoding.Latin1.GetString(bytes),
                CsvTextEncoding.Latin1,
                HadByteOrderMark: false);
        }
    }

    private static string DecodeUtf8Lenient(byte[] bytes, int offset)
    {
        return new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: false)
            .GetString(bytes, offset, bytes.Length - offset);
    }

    private static bool StartsWithUtf8ByteOrderMark(byte[] bytes)
    {
        return bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF;
    }

    private static bool StartsWithUtf16ByteOrderMark(byte[] bytes, out Encoding encoding)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = Encoding.Unicode;
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode;
            return true;
        }

        encoding = Encoding.UTF8;
        return false;
    }
}
