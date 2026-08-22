using System.Reflection;

namespace CashFlowPlanner.Core.Tests.Banking.Camt;

/// <summary>
/// Loads the embedded CAMT.053 fixtures.
///
/// The fixtures follow the real Swiss shape - <c>Document/BkToCstmrStmt/Stmt/Ntry/NtryDtls/TxDtls</c>
/// with Swiss Payment Standards codes and IBANs - so the tests measure the parser against the
/// format rather than against a shape invented to suit the parser.
/// </summary>
internal static class CamtFixture
{
    public const string Plain08 = "Plain.001.08.xml";
    public const string Plain04 = "Plain.001.04.xml";
    public const string BatchBooking = "BatchBooking.001.08.xml";
    public const string Truncated = "Truncated.001.08.xml";
    public const string References = "References.001.08.xml";
    public const string MultiAccount = "MultiAccount.001.08.xml";
    public const string CurrencyFallback = "CurrencyFallback.001.08.xml";
    public const string Charges = "Charges.001.08.xml";
    public const string Malformed = "Malformed.xml";
    public const string NotCamt = "NotCamt.xml";

    private const string ResourcePrefix = "CashFlowPlanner.Core.Tests.Banking.Camt.Fixtures.";

    public static byte[] ReadBytes(string fixtureName)
    {
        var assembly = typeof(CamtFixture).GetTypeInfo().Assembly;

        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + fixtureName)
            ?? throw new InvalidOperationException(
                $"Embedded fixture '{fixtureName}' not found. Available: "
                + string.Join(", ", assembly.GetManifestResourceNames()));

        using var memoryStream = new MemoryStream();

        stream.CopyTo(memoryStream);

        return memoryStream.ToArray();
    }

    public static string ReadText(string fixtureName)
    {
        return System.Text.Encoding.UTF8.GetString(
            ReadBytes(fixtureName));
    }
}
