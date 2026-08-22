namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// Encrypts the browser working copy - the autosaved scratch copy of the plan that lives in
/// localStorage - with a key that belongs to this browser profile and nothing else.
/// <para>
/// <b>Threat model, stated once so nothing downstream has to guess.</b> This protects the bytes
/// at rest in the browser profile directory: a lost laptop, a synced backup, a forensic image, a
/// shared machine. Before it existed, salaries, balances and debts sat in a greppable file.
/// It protects nothing at all against script running on this origin - an XSS payload, a
/// malicious extension, a devtools console - because the page must be able to read its own
/// working copy unattended, so any key it can reach, an attacker in the page can reach too.
/// Do not describe this as XSS mitigation anywhere.
/// </para>
/// <para>
/// The key is a device key, not the file passphrase, and that is a deliberate trade. The working
/// copy exists to be restored on the next page load without prompting anyone; requiring a
/// passphrase to restore it would lose a user's unexported edits whenever they could not be
/// bothered to retype it. The passphrase protects the exported file, which is the thing that
/// actually leaves the machine. See <c>wwwroot/js/working-copy-crypto.js</c>.
/// </para>
/// </summary>
public interface IWorkingCopyCipher
{
    /// <summary>
    /// True once a crypto failure has forced this session onto the plaintext fallback.
    /// </summary>
    bool IsPlaintextFallbackActive { get; }

    /// <summary>
    /// Encrypts a working copy for storage. Never throws, and never returns null or empty for
    /// non-empty input: if no device key can be obtained the plaintext is handed back unchanged
    /// so the caller still writes something.
    /// </summary>
    ValueTask<string> ProtectAsync(string plaintext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts a stored working copy. Values written before this feature existed are plaintext
    /// and pass through unchanged, which is what makes the migration transparent. Returns null
    /// when an envelope cannot be opened - the device key was cleared with site data, or the
    /// value is damaged - because "there is no usable working copy" is a normal state, not an
    /// error: the plan file on disk is the source of truth.
    /// </summary>
    ValueTask<string?> UnprotectAsync(
        string? stored,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The wire marker for an encrypted working copy. Recognising the envelope in C# rather than in
/// JavaScript is what lets the plaintext-migration path be tested without a browser.
/// </summary>
public static class WorkingCopyEnvelope
{
    /// <summary>
    /// Prefix of an encrypted working copy. Must match <c>PREFIX</c> in
    /// <c>wwwroot/js/working-copy-crypto.js</c>.
    /// </summary>
    public const string Prefix = "cfpwc1:";

    /// <summary>
    /// True if this stored value is an encrypted envelope rather than pre-migration plaintext.
    /// A plan document always starts with '{', so the two can never be confused.
    /// </summary>
    public static bool IsEnvelope(string? stored)
    {
        return stored is not null && stored.StartsWith(Prefix, StringComparison.Ordinal);
    }
}

/// <summary>
/// Stores the working copy as-is. Used where encryption is not wanted or not reachable, and by
/// tests that are about something else entirely.
/// </summary>
public sealed class PlaintextWorkingCopyCipher : IWorkingCopyCipher
{
    public static PlaintextWorkingCopyCipher Instance { get; } = new();

    public bool IsPlaintextFallbackActive => true;

    public ValueTask<string> ProtectAsync(
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(plaintext);
    }

    public ValueTask<string?> UnprotectAsync(
        string? stored,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(WorkingCopyEnvelope.IsEnvelope(stored) ? null : stored);
    }
}
