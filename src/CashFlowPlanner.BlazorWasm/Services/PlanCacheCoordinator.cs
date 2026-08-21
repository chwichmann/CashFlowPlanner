using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// Keeps the browser working copy in step with the in-memory plan.
///
/// Autosave used to be <c>_ = SaveCurrentPlanAsync();</c> against a method with a
/// <c>try/finally</c> and no <c>catch</c>, so a failed validation, a full quota or a broken JS
/// interop call vanished into an unobserved task while the navbar kept showing a cache timestamp
/// from an earlier, successful write (finding P1b). Every save now produces a
/// <see cref="PlanSaveResult"/>, and a failure is reported through
/// <see cref="UiFeedbackService"/>.
/// </summary>
public sealed class PlanCacheCoordinator
{
    private readonly CashFlowAppState _appState;
    private readonly CashFlowPlanJsonSerializer _jsonSerializer;
    private readonly IBrowserPlanCache _browserCache;
    private readonly UiFeedbackService _feedback;

    private bool _initialized;
    private bool _isRestoring;
    private bool _isSaving;

    public PlanCacheCoordinator(
        CashFlowAppState appState,
        CashFlowPlanJsonSerializer jsonSerializer,
        IBrowserPlanCache browserCache,
        UiFeedbackService feedback)
    {
        _appState = appState;
        _jsonSerializer = jsonSerializer;
        _browserCache = browserCache;
        _feedback = feedback;
    }

    /// <summary>
    /// The outcome of the most recent save attempt, for anything that wants to show save state.
    /// </summary>
    public PlanSaveResult? LastSaveResult { get; private set; }

    public DateTimeOffset? LastSuccessfulSaveAt { get; private set; }

    /// <summary>
    /// Raised after every save attempt, successful or not.
    /// </summary>
    public event Action? SaveStateChanged;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        _appState.Changed += OnAppStateChanged;

        await RestoreAsync();
    }

    private async Task RestoreAsync()
    {
        if (_appState.CurrentPlan is not null)
        {
            return;
        }

        var cachedJson = await _browserCache.LoadAsync();

        if (string.IsNullOrWhiteSpace(cachedJson))
        {
            return;
        }

        try
        {
            _isRestoring = true;

            var document = _jsonSerializer.DeserializeDocument(cachedJson);
            _appState.LoadDocument(document);
        }
        finally
        {
            _isRestoring = false;
        }
    }

    /// <summary>
    /// Writes the working copy. Never throws: every failure is returned as a
    /// <see cref="PlanSaveResult"/> and reported to the user.
    /// </summary>
    public async Task<PlanSaveResult> SaveCurrentPlanAsync()
    {
        if (_isRestoring || _isSaving)
        {
            return PlanSaveResult.Skipped();
        }

        try
        {
            _isSaving = true;

            var result = await WriteWorkingCopyAsync();

            Report(result);

            return result;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<PlanSaveResult> WriteWorkingCopyAsync()
    {
        try
        {
            if (_appState.CurrentPlan is null)
            {
                await _browserCache.ClearAsync();

                return PlanSaveResult.Cleared();
            }

            var document = _appState.GetDocumentForSave();

            string json;

            try
            {
                json = _jsonSerializer.SerializeDocument(document);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or NotSupportedException)
            {
                // The plan itself is unsavable. This is the loud half of finding P1a: the user has
                // to know immediately, because an export would fail the same way.
                return new PlanSaveResult(
                    PlanSaveOutcome.ValidationFailed,
                    exception.Message,
                    exception);
            }

            var write = await _browserCache.SaveAsync(json);

            if (write.Success)
            {
                return PlanSaveResult.Saved(write.Message);
            }

            return new PlanSaveResult(
                write.Failure switch
                {
                    PlanCacheWriteFailure.QuotaExceeded => PlanSaveOutcome.QuotaExceeded,
                    PlanCacheWriteFailure.StorageUnavailable => PlanSaveOutcome.StorageUnavailable,
                    _ => PlanSaveOutcome.Failed
                },
                write.Message);
        }
        catch (Exception exception)
        {
            return new PlanSaveResult(
                PlanSaveOutcome.Failed,
                exception.Message,
                exception);
        }
    }

    private void Report(PlanSaveResult result)
    {
        LastSaveResult = result;

        if (result.Outcome is PlanSaveOutcome.Saved or PlanSaveOutcome.Cleared)
        {
            LastSuccessfulSaveAt = DateTimeOffset.UtcNow;
        }

        if (result.IsFailure)
        {
            _feedback.Error(result.ToUserMessage());
        }

        SaveStateChanged?.Invoke();
    }

    private void OnAppStateChanged()
    {
        // The event is synchronous, so the task cannot be awaited here. SaveCurrentPlanAsync
        // catches everything, so nothing escapes into an unobserved task.
        _ = SaveCurrentPlanAsync();
    }
}
