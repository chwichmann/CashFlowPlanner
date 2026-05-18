using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class BrowserPlanCacheService
{
    private const string PlanJsonKey = "cashflowplanner.currentPlanJson";
    private const string CachedAtKey = "cashflowplanner.currentPlanCachedAt";

    private readonly IJSRuntime _jsRuntime;

    public BrowserPlanCacheService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SaveAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        await _jsRuntime.InvokeVoidAsync(
            "cashFlowPlanner.setLocalStorageItem",
            cancellationToken,
            PlanJsonKey,
            json);

        await _jsRuntime.InvokeVoidAsync(
            "cashFlowPlanner.setLocalStorageItem",
            cancellationToken,
            CachedAtKey,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    public async Task<string?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        return await _jsRuntime.InvokeAsync<string?>(
            "cashFlowPlanner.getLocalStorageItem",
            cancellationToken,
            PlanJsonKey);
    }

    public async Task<string?> GetCachedAtAsync(
        CancellationToken cancellationToken = default)
    {
        return await _jsRuntime.InvokeAsync<string?>(
            "cashFlowPlanner.getLocalStorageItem",
            cancellationToken,
            CachedAtKey);
    }

    public async Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        await _jsRuntime.InvokeVoidAsync(
            "cashFlowPlanner.removeLocalStorageItem",
            cancellationToken,
            PlanJsonKey);

        await _jsRuntime.InvokeVoidAsync(
            "cashFlowPlanner.removeLocalStorageItem",
            cancellationToken,
            CachedAtKey);
    }
}