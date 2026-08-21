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
public sealed class PlanCacheCoordinator : IDisposable
{
    /// <summary>
    /// Trailing debounce window. Every mutation used to trigger full plan validation plus a full
    /// re-serialize across JS interop - about 288 KB for a realistic plan - so typing in a form
    /// produced one of those per keystroke (finding P3b).
    /// </summary>
    public static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(500);

    private readonly CashFlowAppState _appState;
    private readonly CashFlowPlanJsonSerializer _jsonSerializer;
    private readonly IBrowserPlanCache _browserCache;
    private readonly UiFeedbackService _feedback;
    private readonly TimeSpan _debounceDelay;
    private readonly IUnsavedChangesGuard _unsavedChangesGuard;

    // One save at a time, with a pending flag so the trailing edge always lands. The old code
    // returned early while a save was running, which discarded that change entirely.
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private CancellationTokenSource? _debounceCts;
    private bool _pendingSave;
    private bool _initialized;
    private bool _isRestoring;
    private bool _disposed;

    public PlanCacheCoordinator(
        CashFlowAppState appState,
        CashFlowPlanJsonSerializer jsonSerializer,
        IBrowserPlanCache browserCache,
        UiFeedbackService feedback)
        : this(appState, jsonSerializer, browserCache, feedback, DefaultDebounceDelay, null)
    {
    }

    public PlanCacheCoordinator(
        CashFlowAppState appState,
        CashFlowPlanJsonSerializer jsonSerializer,
        IBrowserPlanCache browserCache,
        UiFeedbackService feedback,
        TimeSpan debounceDelay,
        IUnsavedChangesGuard? unsavedChangesGuard = null)
    {
        _appState = appState;
        _jsonSerializer = jsonSerializer;
        _browserCache = browserCache;
        _feedback = feedback;
        _debounceDelay = debounceDelay;
        _unsavedChangesGuard = unsavedChangesGuard ?? new NullUnsavedChangesGuard();
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

        // PlanChanged, not Changed: running a simulation leaves the plan untouched and must not
        // trigger a write.
        _appState.PlanChanged += OnPlanChanged;
        _appState.DirtyStateChanged += OnDirtyStateChanged;

        await RestoreAsync();

        await _unsavedChangesGuard.SetUnsavedChangesAsync(_appState.IsDirty);
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
    /// Queues a save and returns immediately. Repeated calls inside the debounce window collapse
    /// into a single write; the last one always lands.
    /// </summary>
    public void ScheduleSave()
    {
        if (_isRestoring || _disposed)
        {
            return;
        }

        _pendingSave = true;

        CancelPendingDebounce();

        if (_debounceDelay <= TimeSpan.Zero)
        {
            _ = FlushAsync();
            return;
        }

        var cts = new CancellationTokenSource();
        _debounceCts = cts;

        _ = RunDebouncedSaveAsync(cts.Token);
    }

    private async Task RunDebouncedSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounceDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer change; that call owns the trailing save.
            return;
        }

        await FlushAsync();
    }

    /// <summary>
    /// Writes the working copy now, and keeps writing while further changes arrive during the
    /// write, so the last change is never discarded. Never throws: every failure is returned as a
    /// <see cref="PlanSaveResult"/> and reported to the user.
    /// </summary>
    public async Task<PlanSaveResult> FlushAsync()
    {
        if (_isRestoring)
        {
            return PlanSaveResult.Skipped();
        }

        await _saveGate.WaitAsync();

        try
        {
            var result = PlanSaveResult.Skipped();

            // The trailing edge: a change that arrived while the previous write was in flight
            // sets the flag again and is written by the next turn of this loop.
            while (_pendingSave)
            {
                _pendingSave = false;

                result = await WriteWorkingCopyAsync();

                Report(result);
            }

            return result;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary>
    /// Writes the working copy immediately, bypassing the debounce window.
    /// </summary>
    public Task<PlanSaveResult> SaveCurrentPlanAsync()
    {
        if (_isRestoring)
        {
            return Task.FromResult(PlanSaveResult.Skipped());
        }

        _pendingSave = true;

        CancelPendingDebounce();

        return FlushAsync();
    }

    private void CancelPendingDebounce()
    {
        var pending = _debounceCts;
        _debounceCts = null;

        if (pending is null)
        {
            return;
        }

        pending.Cancel();
        pending.Dispose();
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
                // The JSON already exists here, so re-evaluating the dirty flag is free. This is
                // what lets an undo back to the exported content report clean again.
                _appState.NotifyPersistedContent(json);

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

    private void OnPlanChanged()
    {
        ScheduleSave();
    }

    private void OnDirtyStateChanged()
    {
        _ = _unsavedChangesGuard.SetUnsavedChangesAsync(_appState.IsDirty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _appState.PlanChanged -= OnPlanChanged;
        _appState.DirtyStateChanged -= OnDirtyStateChanged;

        CancelPendingDebounce();

        _saveGate.Dispose();
    }
}
