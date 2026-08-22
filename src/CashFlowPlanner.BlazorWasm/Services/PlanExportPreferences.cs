using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// Whether exported plan files are encrypted.
/// <para>
/// Stored in localStorage rather than in the plan, because the plan itself is what
/// gets encrypted - a setting inside it would be unreadable exactly when it is needed.
/// </para>
/// <para>
/// Defaults to off so that upgrading the app never silently changes what a user's
/// export produces. It turns itself on when an encrypted file is opened, so a plan
/// that arrived encrypted stays encrypted.
/// </para>
/// </summary>
public sealed class PlanExportPreferences
{
    private const string StorageKey = "cashflowplanner.encryptExports";

    private readonly IJSRuntime _jsRuntime;

    public PlanExportPreferences(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action? Changed;

    public bool EncryptExports { get; set; }

    /// <summary>Read the stored preference. Call once at startup.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _jsRuntime.InvokeAsync<string?>(
                "cashFlowPlanner.getLocalStorageItem", StorageKey);

            EncryptExports = string.Equals(stored, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (JSException)
        {
            // Storage unavailable; the default (off) stands.
        }
    }

    public async Task SetAsync(bool encryptExports)
    {
        if (EncryptExports == encryptExports)
        {
            return;
        }

        EncryptExports = encryptExports;

        try
        {
            await _jsRuntime.InvokeAsync<bool>(
                "cashFlowPlanner.setLocalStorageItem",
                StorageKey,
                encryptExports ? "true" : "false");
        }
        catch (JSException)
        {
            // The preference still applies for this session even if it cannot be saved.
        }

        Changed?.Invoke();
    }
}
