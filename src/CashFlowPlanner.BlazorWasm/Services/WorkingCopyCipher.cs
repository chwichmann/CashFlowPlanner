using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// <see cref="IWorkingCopyCipher"/> backed by the browser's own Web Crypto, through
/// <c>wwwroot/js/working-copy-crypto.js</c>.
/// <para>
/// The cryptography lives in JavaScript for the same reason the file format's does: .NET 10 in
/// WebAssembly has no symmetric cipher at all - <c>AesGcm</c>, <c>Aes.Create</c> and friends are
/// <c>[UnsupportedOSPlatform("browser")]</c>. See <c>docs/ENCRYPTED-FILE-FORMAT.md</c>.
/// </para>
/// </summary>
public sealed class WorkingCopyCipher : IWorkingCopyCipher, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;

    private IJSObjectReference? _module;
    private bool _plaintextFallbackActive;
    private bool _warned;

    public WorkingCopyCipher(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public bool IsPlaintextFallbackActive => _plaintextFallbackActive;

    /// <summary>
    /// Encrypts, or gives the plaintext back when the browser will not cooperate.
    /// <para>
    /// <b>Why plaintext and not "refuse to write".</b> The working copy is the only thing standing
    /// between an unexported edit and a closed tab. This project has already paid for the lesson
    /// that a save which quietly does nothing is the worst possible failure - it is finding P1b,
    /// the reason <see cref="PlanSaveResult"/> and <see cref="IBrowserPlanCache"/> exist at all.
    /// Refusing to write when IndexedDB is blocked would reintroduce exactly that: the user would
    /// keep typing, the navbar would keep looking fine, and a reload would silently discard the
    /// session. Degrading to the status quo ante - readable bytes in the profile, plus a loud
    /// console warning - loses no data and leaves the user no worse off than before this feature
    /// existed. Privacy is a real gain; it is not worth buying with someone's afternoon.
    /// </para>
    /// </summary>
    public async ValueTask<string> ProtectAsync(
        string plaintext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        var module = await TryGetModuleAsync(cancellationToken);

        if (module is null)
        {
            return plaintext;
        }

        try
        {
            var envelope = await module.InvokeAsync<string>(
                "protect",
                cancellationToken,
                plaintext);

            if (!WorkingCopyEnvelope.IsEnvelope(envelope))
            {
                // The module handed the plaintext straight back: it could not get a device key
                // and has already warned in the console with the browser-specific reason.
                _plaintextFallbackActive = true;
            }

            return envelope;
        }
        catch (Exception exception) when (IsInteropFailure(exception))
        {
            FallBackToPlaintext(exception);

            return plaintext;
        }
    }

    public async ValueTask<string?> UnprotectAsync(
        string? stored,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        // Pre-migration plaintext never reaches JavaScript. Recognising it here keeps a returning
        // user's plan readable even in the session where crypto is broken.
        if (!WorkingCopyEnvelope.IsEnvelope(stored))
        {
            return stored;
        }

        var module = await TryGetModuleAsync(cancellationToken);

        if (module is null)
        {
            return null;
        }

        try
        {
            return await module.InvokeAsync<string?>("unprotect", cancellationToken, stored);
        }
        catch (Exception exception) when (IsInteropFailure(exception))
        {
            FallBackToPlaintext(exception);

            return null;
        }
    }

    private async ValueTask<IJSObjectReference?> TryGetModuleAsync(
        CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        if (_plaintextFallbackActive)
        {
            // Already established that this browser cannot do it. Do not re-import per save.
            return null;
        }

        try
        {
            // "./" resolves against the document base, so this works at both / and
            // /CashFlowPlanner/ without the deploy workflow having to rewrite it.
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                cancellationToken,
                "./js/working-copy-crypto.js");

            return _module;
        }
        catch (Exception exception) when (IsInteropFailure(exception))
        {
            FallBackToPlaintext(exception);

            return null;
        }
    }

    /// <summary>
    /// Everything JS interop can plausibly throw when the browser is missing, shutting down, or
    /// refusing. Deliberately not a blanket catch: a bug in the module should still surface.
    /// </summary>
    private static bool IsInteropFailure(Exception exception)
    {
        return exception
            is JSException
            or JSDisconnectedException
            or InvalidOperationException
            or ObjectDisposedException
            or NotSupportedException;
    }

    private void FallBackToPlaintext(Exception exception)
    {
        _plaintextFallbackActive = true;

        if (_warned)
        {
            return;
        }

        _warned = true;

        // Console.Error in WebAssembly lands in the browser console. Once per session: this fires
        // on every autosave otherwise, and a warning nobody can scroll past is a warning nobody
        // reads.
        Console.Error.WriteLine(
            "[CashFlow Planner] The browser working copy could not be encrypted and is being "
            + "stored unencrypted in localStorage, because the device key store is unavailable "
            + $"({exception.GetType().Name}: {exception.Message}). No data is lost. The working "
            + "copy is readable by anything that can read this browser profile until the key "
            + "store works again; your exported plan file is unaffected.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is going away, which disposes the module anyway.
        }

        _module = null;
    }
}
