using CashFlowPlanner.BlazorWasm.Services;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// An in-memory stand-in for localStorage. This is what the IBrowserPlanCache extraction bought:
/// the autosave, quota and dropped-save paths can be driven without a browser.
/// </summary>
internal sealed class FakeBrowserPlanCache : IBrowserPlanCache
{
    private string? _current;
    private string? _previous;
    private string? _cachedAt;

    /// <summary>Every JSON payload handed to SaveAsync, in order.</summary>
    public List<string> Writes { get; } = [];

    public int ClearCount { get; private set; }

    /// <summary>When set, the next SaveAsync fails with this result and writes nothing.</summary>
    public PlanCacheWriteResult? NextWriteResult { get; set; }

    /// <summary>When set, every SaveAsync fails with this result and writes nothing.</summary>
    public PlanCacheWriteResult? PermanentWriteResult { get; set; }

    /// <summary>When set, SaveAsync throws this before doing anything.</summary>
    public Exception? ThrowOnSave { get; set; }

    /// <summary>Awaited inside SaveAsync, to hold a save open while another change arrives.</summary>
    public TaskCompletionSource? SaveGate { get; set; }

    public async Task<PlanCacheWriteResult> SaveAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave is not null)
        {
            throw ThrowOnSave;
        }

        if (SaveGate is not null)
        {
            await SaveGate.Task;
        }

        var failure = PermanentWriteResult ?? NextWriteResult;

        if (failure is not null)
        {
            NextWriteResult = null;

            return failure;
        }

        Writes.Add(json);

        _previous = _current;
        _current = json;
        _cachedAt = DateTimeOffset.UtcNow.ToString("O");

        return PlanCacheWriteResult.Ok();
    }

    public Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_current);
    }

    public Task<string?> LoadPreviousAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_previous);
    }

    public Task<string?> GetCachedAtAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cachedAt);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ClearCount++;

        _current = null;
        _previous = null;
        _cachedAt = null;

        return Task.CompletedTask;
    }

    public void Seed(string json)
    {
        _current = json;
    }
}
