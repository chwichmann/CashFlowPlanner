using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>The user closed the passphrase prompt instead of answering it.</summary>
public sealed class PassphraseCancelledException : Exception
{
    public PassphraseCancelledException()
        : base("Cancelled.")
    {
    }
}

/// <summary>
/// One place that knows how to turn file text into a plan and a plan into file text,
/// including the encrypted case.
/// <para>
/// Both the navigation bar and the empty-state screen open files. Before this existed
/// each had its own copy of the read-and-deserialize logic, which is exactly how the
/// two drifted apart on error handling.
/// </para>
/// </summary>
public sealed class PlanFileService
{
    private readonly CashFlowPlanJsonSerializer _serializer;
    private readonly PlanCryptoService _crypto;
    private readonly PassphrasePromptService _prompt;
    private readonly PlanExportPreferences _preferences;

    public PlanFileService(
        CashFlowPlanJsonSerializer serializer,
        PlanCryptoService crypto,
        PassphrasePromptService prompt,
        PlanExportPreferences preferences)
    {
        _serializer = serializer;
        _crypto = crypto;
        _prompt = prompt;
        _preferences = preferences;
    }

    /// <summary>
    /// Read a plan out of file text, decrypting first if it is encrypted, prompting for
    /// the passphrase and re-prompting on a wrong one.
    /// </summary>
    /// <exception cref="PassphraseCancelledException">The user dismissed the prompt.</exception>
    public async Task<CashFlowPlanDocument> ReadAsync(string fileText, string? fileName = null)
    {
        if (!await _crypto.IsEncryptedAsync(fileText))
        {
            return _serializer.DeserializeDocument(fileText);
        }

        // Throws with an actionable message for a file from a newer format version.
        var salt = await _crypto.ReadSaltAsync(fileText);

        string? error = null;

        while (true)
        {
            var passphrase = await _prompt.RequestAsync(
                PassphrasePromptMode.Unlock, fileName, error);

            if (passphrase is null)
            {
                throw new PassphraseCancelledException();
            }

            await _crypto.UnlockAsync(passphrase, salt);

            try
            {
                var planJson = await _crypto.DecryptAsync(fileText);

                // Opening an encrypted file means this plan stays encrypted on save,
                // which is what the user would expect and the safer default. Persist it,
                // so it survives a reload rather than silently reverting to plaintext.
                await _preferences.SetAsync(true);

                return _serializer.DeserializeDocument(planJson);
            }
            catch (Exception ex)
            {
                // A wrong passphrase is the overwhelmingly likely cause; re-ask rather
                // than dumping the user back to an empty screen.
                await _crypto.LockAsync();
                error = ex.Message;
            }
        }
    }

    /// <summary>
    /// Produce the bytes to write for the current plan, encrypting when the user has
    /// asked for it, prompting for a passphrase the first time.
    /// </summary>
    /// <exception cref="PassphraseCancelledException">The user dismissed the prompt.</exception>
    public async Task<PlanFileContent> WriteAsync(CashFlowPlanDocument document)
    {
        var planJson = _serializer.SerializeDocument(document);

        if (!_preferences.EncryptExports)
        {
            return new PlanFileContent(planJson, IsEncrypted: false);
        }

        if (!_crypto.IsUnlocked)
        {
            var passphrase = await _prompt.RequestAsync(PassphrasePromptMode.Create);

            if (passphrase is null)
            {
                throw new PassphraseCancelledException();
            }

            await _crypto.UnlockAsync(passphrase, salt: null);
        }

        var envelope = await _crypto.EncryptAsync(planJson);

        return new PlanFileContent(envelope, IsEncrypted: true);
    }
}

/// <summary>File text plus whether it ended up encrypted, which decides the extension.</summary>
public sealed record PlanFileContent(string Text, bool IsEncrypted)
{
    public string Extension => IsEncrypted ? ".cfplan" : ".json";

    public string ContentType => "application/json";
}
