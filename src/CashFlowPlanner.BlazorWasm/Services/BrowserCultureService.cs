using System.Text.Json;
using CashFlowPlanner.BlazorWasm.Models;
using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class BrowserCultureService
{
    private const string CulturePreferencesKey = "cashflowplanner.culturePreferences";

    private readonly IJSRuntime _jsRuntime;

    public BrowserCultureService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<CulturePreferences> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var json = await _jsRuntime.InvokeAsync<string?>(
            "cashFlowPlanner.getLocalStorageItem",
            cancellationToken,
            CulturePreferencesKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new CulturePreferences();
        }

        try
        {
            return JsonSerializer.Deserialize<CulturePreferences>(json)
                ?? new CulturePreferences();
        }
        catch
        {
            return new CulturePreferences();
        }
    }

    public async Task SaveAsync(
        CulturePreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(preferences);

        await _jsRuntime.InvokeVoidAsync(
            "cashFlowPlanner.setLocalStorageItem",
            cancellationToken,
            CulturePreferencesKey,
            json);
    }
}