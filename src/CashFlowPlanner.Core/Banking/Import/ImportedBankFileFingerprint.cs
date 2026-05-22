using System.Security.Cryptography;

namespace CashFlowPlanner.Core.Banking.Import;

public static class ImportedBankFileFingerprint
{
    public static string Create(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        var hash = SHA256.HashData(fileBytes);

        return Convert.ToHexString(hash);
    }

    public static string Create(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = System.Text.Encoding.UTF8.GetBytes(text);

        return Create(bytes);
    }
}