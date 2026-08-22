using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// The localStorage-backed working copy.
/// <para>
/// The plan JSON and its <c>.prev</c> recovery copy are encrypted at rest with a device key held
/// in IndexedDB - see <see cref="IWorkingCopyCipher"/> for what that does and does not protect.
/// Encryption sits here, beneath <see cref="IBrowserPlanCache"/>, rather than in
/// <see cref="PlanCacheCoordinator"/>: the coordinator's debounce, trailing write and
/// <see cref="PlanSaveResult"/> reporting are load-bearing and must not learn about ciphertext.
/// </para>
/// <para>
/// The timestamp key stays in the clear on purpose. It is a wall-clock time the navbar reads on
/// startup, it reveals nothing about the household's finances, and encrypting it would make the
/// "when was this cached" display depend on the key store being healthy.
/// </para>
/// </summary>
public sealed class BrowserPlanCacheService : IBrowserPlanCache
{
    private const string PlanJsonKey = "cashflowplanner.currentPlanJson";
    private const string PreviousPlanJsonKey = "cashflowplanner.currentPlanJson.prev";
    private const string CachedAtKey = "cashflowplanner.currentPlanCachedAt";

    private readonly IJSRuntime _jsRuntime;
    private readonly IWorkingCopyCipher _cipher;

    public BrowserPlanCacheService(IJSRuntime jsRuntime, IWorkingCopyCipher cipher)
    {
        _jsRuntime = jsRuntime;
        _cipher = cipher;
    }

    /// <summary>
    /// Writes the working copy. localStorage has no transaction, so the order is chosen so that a
    /// failure always leaves a readable plan behind:
    /// the previous good copy is rotated first, then the plan, then the timestamp.
    /// A failed plan write leaves the old plan in place; a failed timestamp write is reported but
    /// does not lose data.
    /// </summary>
    public async Task<PlanCacheWriteResult> SaveAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return PlanCacheWriteResult.Ok();
        }

        var previousStored = await GetItemAsync(PlanJsonKey, cancellationToken);

        // The "has anything actually changed" test has to run on plaintext. Every encrypted write
        // gets a fresh IV, so two saves of an identical plan produce different ciphertext; a
        // ciphertext comparison would rotate on every save and destroy the recovery point.
        var previousPlain = await _cipher.UnprotectAsync(previousStored, cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousPlain) &&
            !string.Equals(previousPlain, json, StringComparison.Ordinal))
        {
            // Moved across as stored: it is already protected, and the envelope is not bound to
            // the slot it sits in, so no re-encryption is needed.
            // Best effort: a failed rotation must never block the real write.
            await SetItemAsync(PreviousPlanJsonKey, previousStored!, cancellationToken);
        }

        var protectedJson = await _cipher.ProtectAsync(json, cancellationToken);

        var planWrite = await SetItemAsync(PlanJsonKey, protectedJson, cancellationToken);

        if (!planWrite.Success && planWrite.Failure == PlanCacheWriteFailure.QuotaExceeded)
        {
            // The recovery copy is the least valuable thing in storage. Trade it for the plan.
            await RemoveItemAsync(PreviousPlanJsonKey, cancellationToken);

            planWrite = await SetItemAsync(PlanJsonKey, protectedJson, cancellationToken);
        }

        if (!planWrite.Success)
        {
            return planWrite;
        }

        var timestampWrite = await SetItemAsync(
            CachedAtKey,
            DateTimeOffset.UtcNow.ToString("O"),
            cancellationToken);

        return timestampWrite.Success
            ? PlanCacheWriteResult.Ok()
            : PlanCacheWriteResult.Ok(
                "The plan was written to the browser working copy but its timestamp was not " +
                "updated, so the displayed cache time may be stale.");
    }

    /// <summary>
    /// Reads the working copy, decrypting it if it is an envelope.
    /// <para>
    /// A returning user has plaintext JSON sitting under this key right now. It is returned as-is
    /// and then rewritten encrypted straight away, rather than waiting for the next edit: someone
    /// who opens the app to look at last month's numbers and never types anything would otherwise
    /// keep a readable plan in their profile forever. The rewrite is best effort and deliberately
    /// unreported - the plaintext stays put until the encrypted write actually succeeds, so the
    /// worst case is that nothing changes.
    /// </para>
    /// </summary>
    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetItemAsync(PlanJsonKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        var plain = await _cipher.UnprotectAsync(stored, cancellationToken);

        if (plain is null || WorkingCopyEnvelope.IsEnvelope(stored))
        {
            return plain;
        }

        await UpgradePlaintextInPlaceAsync(PlanJsonKey, plain, cancellationToken);

        return plain;
    }

    public async Task<string?> LoadPreviousAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetItemAsync(PreviousPlanJsonKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        var plain = await _cipher.UnprotectAsync(stored, cancellationToken);

        if (plain is null || WorkingCopyEnvelope.IsEnvelope(stored))
        {
            return plain;
        }

        await UpgradePlaintextInPlaceAsync(PreviousPlanJsonKey, plain, cancellationToken);

        return plain;
    }

    /// <summary>
    /// Replaces a plaintext value with its encrypted form under the same key. No rotation: the
    /// content is identical, only its representation changes.
    /// </summary>
    private async Task UpgradePlaintextInPlaceAsync(
        string key,
        string plain,
        CancellationToken cancellationToken)
    {
        var protectedValue = await _cipher.ProtectAsync(plain, cancellationToken);

        if (!WorkingCopyEnvelope.IsEnvelope(protectedValue))
        {
            // No device key available. Leave the plaintext exactly where it is; rewriting it with
            // itself would only risk a failed write for no benefit.
            return;
        }

        await SetItemAsync(key, protectedValue, cancellationToken);
    }

    public Task<string?> GetCachedAtAsync(CancellationToken cancellationToken = default)
    {
        return GetItemAsync(CachedAtKey, cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await RemoveItemAsync(PlanJsonKey, cancellationToken);
        await RemoveItemAsync(PreviousPlanJsonKey, cancellationToken);
        await RemoveItemAsync(CachedAtKey, cancellationToken);
    }

    private async Task<string?> GetItemAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>(
                "cashFlowPlanner.getLocalStorageItem",
                cancellationToken,
                key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task<PlanCacheWriteResult> SetItemAsync(
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        bool written;

        try
        {
            written = await _jsRuntime.InvokeAsync<bool>(
                "cashFlowPlanner.setLocalStorageItem",
                cancellationToken,
                key,
                value);
        }
        catch (JSException exception)
        {
            return PlanCacheWriteResult.Failed(
                PlanCacheWriteFailure.StorageUnavailable,
                $"Browser storage is not available: {exception.Message}");
        }

        if (written)
        {
            return PlanCacheWriteResult.Ok();
        }

        var error = await GetLastStorageErrorAsync(cancellationToken);

        if (error?.IsQuotaExceeded == true)
        {
            return PlanCacheWriteResult.Failed(
                PlanCacheWriteFailure.QuotaExceeded,
                "Browser storage is full.");
        }

        return PlanCacheWriteResult.Failed(
            PlanCacheWriteFailure.StorageUnavailable,
            error?.Message is { Length: > 0 } message
                ? $"Browser storage rejected the write: {message}"
                : "Browser storage rejected the write.");
    }

    private async Task RemoveItemAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "cashFlowPlanner.removeLocalStorageItem",
                cancellationToken,
                key);
        }
        catch (JSException)
        {
            // Nothing useful to do: the value stays, and the caller already knows storage is sick.
        }
    }

    private async Task<BrowserStorageError?> GetLastStorageErrorAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<BrowserStorageError?>(
                "cashFlowPlanner.getLastStorageError",
                cancellationToken);
        }
        catch (JSException)
        {
            return null;
        }
    }
}
