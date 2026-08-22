using System.Text;
using CashFlowPlanner.BlazorWasm.Services;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Stands in for the real AES-GCM device-key cipher, which cannot run here: Web Crypto and
/// IndexedDB do not exist in the xUnit process. This is deliberately <b>not</b> cryptography - it
/// is a reversible XOR, and it would be worthless in production - but it has the two properties
/// the cache tests actually depend on: the stored form is an envelope carrying no readable plan
/// content, and only this object can turn it back into JSON.
/// <para>
/// The real thing is exercised in a browser by <c>tools/working-copy-crypto-selftest.html</c>.
/// </para>
/// </summary>
internal sealed class FakeWorkingCopyCipher : IWorkingCopyCipher
{
    private const byte Mask = 0x5A;

    /// <summary>Simulates a browser that will not give us a device key at all.</summary>
    public bool CryptoUnavailable { get; set; }

    /// <summary>
    /// Simulates the device key having been cleared with site data: existing envelopes can no
    /// longer be opened, but new writes work.
    /// </summary>
    public bool DeviceKeyLost { get; set; }

    public int ProtectCount { get; private set; }

    public bool IsPlaintextFallbackActive => CryptoUnavailable;

    public ValueTask<string> ProtectAsync(
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        ProtectCount++;

        if (CryptoUnavailable || string.IsNullOrEmpty(plaintext))
        {
            return ValueTask.FromResult(plaintext);
        }

        var bytes = Encoding.UTF8.GetBytes(plaintext);

        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= Mask;
        }

        return ValueTask.FromResult(
            WorkingCopyEnvelope.Prefix + Convert.ToBase64String(bytes));
    }

    public ValueTask<string?> UnprotectAsync(
        string? stored,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return ValueTask.FromResult<string?>(null);
        }

        // Pre-migration plaintext passes through, exactly as the real cipher does.
        if (!WorkingCopyEnvelope.IsEnvelope(stored))
        {
            return ValueTask.FromResult<string?>(stored);
        }

        if (CryptoUnavailable || DeviceKeyLost)
        {
            return ValueTask.FromResult<string?>(null);
        }

        var bytes = Convert.FromBase64String(stored[WorkingCopyEnvelope.Prefix.Length..]);

        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= Mask;
        }

        return ValueTask.FromResult<string?>(Encoding.UTF8.GetString(bytes));
    }
}
