using CashFlowPlanner.BlazorWasm.Services;
using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// An in-memory stand-in for the <c>window.cashFlowPlanner</c> localStorage shim in
/// <c>wwwroot/index.html</c>, so <see cref="BrowserPlanCacheService"/> itself - not just the
/// interface in front of it - can be tested. <see cref="FakeBrowserPlanCache"/> replaces the whole
/// cache; this replaces only the browser under it, which is what the encryption tests need:
/// they have to see the exact bytes that reach storage.
/// </summary>
internal sealed class FakeLocalStorageJsRuntime : IJSRuntime
{
    /// <summary>The raw stored values, exactly as localStorage would hold them.</summary>
    public Dictionary<string, string> Items { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, every write is refused the way a full quota would refuse it.</summary>
    public bool QuotaExceeded { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        var arguments = args ?? [];

        object? result = identifier switch
        {
            "cashFlowPlanner.getLocalStorageItem" =>
                Items.TryGetValue(Key(arguments, 0), out var value) ? value : null,

            "cashFlowPlanner.setLocalStorageItem" => Set(Key(arguments, 0), Key(arguments, 1)),

            "cashFlowPlanner.removeLocalStorageItem" => Remove(Key(arguments, 0)),

            // The bank-import store talks to localStorage directly rather than through the
            // shim. Same storage, so the same dictionary answers for it.
            "localStorage.getItem" =>
                Items.TryGetValue(Key(arguments, 0), out var raw) ? raw : null,

            "localStorage.setItem" => Set(Key(arguments, 0), Key(arguments, 1)),

            "localStorage.removeItem" => Remove(Key(arguments, 0)),

            "cashFlowPlanner.getLastStorageError" => QuotaExceeded
                ? new BrowserStorageError("QuotaExceededError", "Browser storage is full.", true)
                : null,

            _ => throw new InvalidOperationException(
                $"The test reached an unexpected JavaScript call: {identifier}")
        };

        return ValueTask.FromResult(result is TValue typed ? typed : default!);
    }

    private static string Key(object?[] args, int index)
    {
        return args.Length > index ? args[index] as string ?? string.Empty : string.Empty;
    }

    private object Set(string key, string value)
    {
        if (QuotaExceeded)
        {
            return false;
        }

        Items[key] = value;

        return true;
    }

    private object? Remove(string key)
    {
        Items.Remove(key);

        // InvokeVoidAsync goes through InvokeAsync<IJSVoidResult>; null is the right answer.
        return null;
    }
}
