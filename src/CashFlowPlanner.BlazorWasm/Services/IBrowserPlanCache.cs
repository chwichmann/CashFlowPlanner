namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// The browser-side working copy of the plan. Extracted from
/// <see cref="BrowserPlanCacheService"/> so that the autosave, quota and dropped-save paths can be
/// tested without a browser (finding P1b).
/// </summary>
public interface IBrowserPlanCache
{
    /// <summary>
    /// Writes the working copy. Never throws for an expected storage failure: a full quota or a
    /// blocked localStorage is reported through the result so the caller can tell the user.
    /// </summary>
    Task<PlanCacheWriteResult> SaveAsync(
        string json,
        CancellationToken cancellationToken = default);

    Task<string?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The previous good working copy, kept as a one-step recovery point.
    /// </summary>
    Task<string?> LoadPreviousAsync(CancellationToken cancellationToken = default);

    Task<string?> GetCachedAtAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Why a write to the browser working copy failed.
/// </summary>
public enum PlanCacheWriteFailure
{
    None = 0,

    /// <summary>
    /// localStorage rejected the write because the origin is out of space. The user's data is not
    /// lost, but it only exists in memory.
    /// </summary>
    QuotaExceeded = 1,

    /// <summary>
    /// localStorage is unavailable or blocked (private mode, disabled site data, JS interop down).
    /// </summary>
    StorageUnavailable = 2
}

/// <summary>
/// The outcome of a write to the browser working copy.
/// </summary>
public sealed record PlanCacheWriteResult(
    bool Success,
    PlanCacheWriteFailure Failure,
    string? Message)
{
    public static PlanCacheWriteResult Ok(string? message = null)
    {
        return new PlanCacheWriteResult(true, PlanCacheWriteFailure.None, message);
    }

    public static PlanCacheWriteResult Failed(
        PlanCacheWriteFailure failure,
        string message)
    {
        return new PlanCacheWriteResult(false, failure, message);
    }
}

/// <summary>
/// Diagnostics for the last failed localStorage write, read back from the JS shim.
/// </summary>
public sealed record BrowserStorageError(
    string? Name,
    string? Message,
    bool IsQuotaExceeded);
