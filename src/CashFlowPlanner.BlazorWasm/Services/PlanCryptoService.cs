using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// Encrypts and decrypts plan files.
/// <para>
/// The cryptography itself lives in <c>wwwroot/js/plan-crypto.js</c> because .NET in
/// WebAssembly has no symmetric cipher at all - <c>AesGcm</c>, <c>Aes.Create</c> and
/// friends are all <c>[UnsupportedOSPlatform("browser")]</c>. See
/// <c>docs/ENCRYPTED-FILE-FORMAT.md</c> for the format and the reasoning.
/// </para>
/// <para>
/// The derived key is held in JavaScript for the lifetime of the tab and is never
/// persisted. Closing the tab locks the app; there is no "remember me".
/// </para>
/// </summary>
public sealed class PlanCryptoService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public PlanCryptoService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Raised when the lock state changes, so the UI can reflect it.</summary>
    public event Action? LockStateChanged;

    /// <summary>True once a passphrase has been accepted this session.</summary>
    public bool IsUnlocked { get; private set; }

    /// <summary>
    /// The salt of the currently unlocked file, needed to keep the cached key valid
    /// across saves. Null when locked.
    /// </summary>
    public string? CurrentSalt { get; private set; }

    private async Task<IJSObjectReference> ModuleAsync()
    {
        // "./" resolves against the document base, so this works at both / and
        // /CashFlowPlanner/ without the workflow having to rewrite it.
        return _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/plan-crypto.js");
    }

    /// <summary>
    /// Derive and cache the key. Pass the salt from an existing file to open it, or
    /// null to start a new encrypted file with a fresh salt.
    /// </summary>
    /// <returns>The salt in use, which must be kept for subsequent saves.</returns>
    public async Task<string> UnlockAsync(string passphrase, string? salt)
    {
        var module = await ModuleAsync();

        CurrentSalt = await module.InvokeAsync<string>("unlock", passphrase, salt);
        IsUnlocked = true;

        LockStateChanged?.Invoke();

        return CurrentSalt;
    }

    /// <summary>Forget the derived key. The next save or open needs the passphrase again.</summary>
    public async Task LockAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("lock");
        }

        IsUnlocked = false;
        CurrentSalt = null;

        LockStateChanged?.Invoke();
    }

    /// <summary>Does this text look like an encrypted plan? Does not attempt to decrypt.</summary>
    public async Task<bool> IsEncryptedAsync(string text)
    {
        var module = await ModuleAsync();

        return await module.InvokeAsync<bool>("isEncrypted", text);
    }

    /// <summary>Read the salt out of an encrypted file so <see cref="UnlockAsync"/> can use it.</summary>
    public async Task<string> ReadSaltAsync(string envelopeJson)
    {
        var module = await ModuleAsync();

        return await module.InvokeAsync<string>("readSalt", envelopeJson);
    }

    /// <summary>Encrypt plan JSON. Requires <see cref="UnlockAsync"/> first.</summary>
    public async Task<string> EncryptAsync(string planJson)
    {
        var module = await ModuleAsync();

        return await module.InvokeAsync<string>("encrypt", planJson);
    }

    /// <summary>Decrypt an envelope. Requires <see cref="UnlockAsync"/> with the file's salt.</summary>
    public async Task<string> DecryptAsync(string envelopeJson)
    {
        var module = await ModuleAsync();

        return await module.InvokeAsync<string>("decrypt", envelopeJson);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("lock");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away, which locks everything anyway.
            }
        }
    }
}
