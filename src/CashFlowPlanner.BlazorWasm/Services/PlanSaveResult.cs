namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// How the last attempt to persist the working copy ended.
/// </summary>
public enum PlanSaveOutcome
{
    /// <summary>The working copy was written.</summary>
    Saved = 0,

    /// <summary>Nothing to do - no plan is loaded, or a restore is in progress.</summary>
    Skipped = 1,

    /// <summary>No plan is loaded any more, so the working copy was removed.</summary>
    Cleared = 2,

    /// <summary>The plan in memory does not validate and therefore cannot be written at all.</summary>
    ValidationFailed = 3,

    /// <summary>Browser storage is full.</summary>
    QuotaExceeded = 4,

    /// <summary>Browser storage is unavailable or refused the write.</summary>
    StorageUnavailable = 5,

    /// <summary>Something else went wrong while producing or writing the JSON.</summary>
    Failed = 6
}

/// <summary>
/// The result of one autosave attempt. Autosave used to be fire-and-forget with no catch, so a
/// failing save was invisible while the UI still claimed the plan was cached (finding P1b).
/// </summary>
public sealed record PlanSaveResult(
    PlanSaveOutcome Outcome,
    string? Message = null,
    Exception? Exception = null)
{
    public bool IsFailure =>
        Outcome is not (PlanSaveOutcome.Saved or PlanSaveOutcome.Skipped or PlanSaveOutcome.Cleared);

    public static PlanSaveResult Saved(string? message = null)
    {
        return new PlanSaveResult(PlanSaveOutcome.Saved, message);
    }

    public static PlanSaveResult Skipped()
    {
        return new PlanSaveResult(PlanSaveOutcome.Skipped);
    }

    public static PlanSaveResult Cleared()
    {
        return new PlanSaveResult(PlanSaveOutcome.Cleared);
    }

    /// <summary>
    /// The user-facing sentence for a failure. Every variant says the same crucial thing: the work
    /// is still in memory and exporting to a file is the way to secure it.
    /// </summary>
    public string ToUserMessage()
    {
        return Outcome switch
        {
            PlanSaveOutcome.QuotaExceeded =>
                "The browser working copy could not be saved: browser storage is full. " +
                "Export the plan to a file now - your recent changes exist only in this tab.",

            PlanSaveOutcome.StorageUnavailable =>
                "The browser working copy could not be saved: browser storage is unavailable. " +
                "Export the plan to a file now - your recent changes exist only in this tab. " +
                $"({Message})",

            PlanSaveOutcome.ValidationFailed =>
                "The browser working copy could not be saved because the plan is not valid: " +
                $"{Message} Fix this before closing the tab - the plan cannot be exported either " +
                "while it is invalid.",

            PlanSaveOutcome.Failed =>
                $"The browser working copy could not be saved: {Message} " +
                "Export the plan to a file now - your recent changes exist only in this tab.",

            _ =>
                Message ?? string.Empty
        };
    }
}
