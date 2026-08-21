using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// Pushes the unsaved-changes flag to whatever can warn the user before the work disappears
/// (finding P1c). Abstracted so that state and coordinator tests run without a browser.
/// </summary>
public interface IUnsavedChangesGuard
{
    Task SetUnsavedChangesAsync(bool hasUnsavedChanges);
}

/// <summary>
/// Drives the <c>beforeunload</c> handler registered in index.html.
/// </summary>
public sealed class BrowserUnsavedChangesGuard : IUnsavedChangesGuard
{
    private readonly IJSRuntime _jsRuntime;

    private bool? _lastPushedValue;

    public BrowserUnsavedChangesGuard(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SetUnsavedChangesAsync(bool hasUnsavedChanges)
    {
        if (_lastPushedValue == hasUnsavedChanges)
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "cashFlowPlanner.setUnsavedChanges",
                hasUnsavedChanges);

            _lastPushedValue = hasUnsavedChanges;
        }
        catch (JSException)
        {
            // Losing the unload warning must never break the app; retry on the next flip.
            _lastPushedValue = null;
        }
    }
}

/// <summary>
/// No-op guard for tests and for any host without JS interop.
/// </summary>
public sealed class NullUnsavedChangesGuard : IUnsavedChangesGuard
{
    public bool? LastValue { get; private set; }

    public Task SetUnsavedChangesAsync(bool hasUnsavedChanges)
    {
        LastValue = hasUnsavedChanges;

        return Task.CompletedTask;
    }
}
