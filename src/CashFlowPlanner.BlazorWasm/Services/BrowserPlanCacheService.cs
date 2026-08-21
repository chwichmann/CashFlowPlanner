using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class BrowserPlanCacheService : IBrowserPlanCache
{
    private const string PlanJsonKey = "cashflowplanner.currentPlanJson";
    private const string PreviousPlanJsonKey = "cashflowplanner.currentPlanJson.prev";
    private const string CachedAtKey = "cashflowplanner.currentPlanCachedAt";

    private readonly IJSRuntime _jsRuntime;

    public BrowserPlanCacheService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
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

        var previous = await GetItemAsync(PlanJsonKey, cancellationToken);

        if (!string.IsNullOrWhiteSpace(previous) &&
            !string.Equals(previous, json, StringComparison.Ordinal))
        {
            // Best effort: a failed rotation must never block the real write.
            await SetItemAsync(PreviousPlanJsonKey, previous, cancellationToken);
        }

        var planWrite = await SetItemAsync(PlanJsonKey, json, cancellationToken);

        if (!planWrite.Success && planWrite.Failure == PlanCacheWriteFailure.QuotaExceeded)
        {
            // The recovery copy is the least valuable thing in storage. Trade it for the plan.
            await RemoveItemAsync(PreviousPlanJsonKey, cancellationToken);

            planWrite = await SetItemAsync(PlanJsonKey, json, cancellationToken);
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

    public Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return GetItemAsync(PlanJsonKey, cancellationToken);
    }

    public Task<string?> LoadPreviousAsync(CancellationToken cancellationToken = default)
    {
        return GetItemAsync(PreviousPlanJsonKey, cancellationToken);
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
