namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// Keeps the linked file on disk up to date with the plan in memory.
/// <para>
/// Deliberately separate from <see cref="PlanCacheCoordinator"/>. That one owns the browser
/// working copy, is heavily tested, and must stay fast and reliable; disk I/O is slower, can
/// fail for reasons the browser copy never does (a revoked grant, a removed drive), and is
/// optional. Threading it through the working-copy path would have coupled the reliable thing
/// to the unreliable one.
/// </para>
/// <para>
/// Runs on a longer debounce than the working copy for the same reason: a real file write is
/// heavier than a localStorage write, and there is no value in touching the disk on every
/// keystroke.
/// </para>
/// </summary>
public sealed class DiskAutoSaveCoordinator : IDisposable
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(1500);

    private readonly CashFlowAppState _appState;
    private readonly PlanFileService _planFiles;
    private readonly DiskAutoSaveService _disk;
    private readonly UiFeedbackService _feedback;
    private readonly TimeSpan _debounceDelay;

    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private CancellationTokenSource? _debounceCts;
    private bool _pendingWrite;

    // Reported once per lapse, not once per debounce. A revoked grant would otherwise raise a
    // banner every time the user typed.
    private bool _reportedPermissionLapse;

    public DiskAutoSaveCoordinator(
        CashFlowAppState appState,
        PlanFileService planFiles,
        DiskAutoSaveService disk,
        UiFeedbackService feedback)
        : this(appState, planFiles, disk, feedback, DefaultDebounceDelay)
    {
    }

    public DiskAutoSaveCoordinator(
        CashFlowAppState appState,
        PlanFileService planFiles,
        DiskAutoSaveService disk,
        UiFeedbackService feedback,
        TimeSpan debounceDelay)
    {
        _appState = appState;
        _planFiles = planFiles;
        _disk = disk;
        _feedback = feedback;
        _debounceDelay = debounceDelay;
    }

    /// <summary>When the linked file was last written in this session.</summary>
    public DateTimeOffset? LastWrittenAt { get; private set; }

    /// <summary>
    /// True when the plan has changed since the last successful disk write - including because
    /// the plan is encrypted and locked, so it could not be written without prompting.
    /// </summary>
    public bool FileIsBehind { get; private set; }

    public event Action? Changed;

    public void Initialize()
    {
        // PlanChanged, not Changed: running a simulation leaves the plan byte-for-byte identical,
        // and there is nothing to write.
        _appState.PlanChanged += OnPlanChanged;
    }

    private void OnPlanChanged()
    {
        if (!_disk.Status.CanWrite)
        {
            // Nothing linked, or the grant lapsed. Still mark the file stale so the UI can say so.
            if (_disk.Status.State != DiskLinkState.Unsupported && _disk.Status.State != DiskLinkState.Unlinked)
            {
                SetBehind(true);
            }

            return;
        }

        SetBehind(true);
        _pendingWrite = true;

        _ = DebounceAsync();
    }

    private async Task DebounceAsync()
    {
        var previous = _debounceCts;
        _debounceCts = new CancellationTokenSource();

        if (previous is not null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(_debounceDelay, token);
        }
        catch (OperationCanceledException)
        {
            // Another change arrived; that one owns the write.
            return;
        }

        await FlushAsync();
    }

    /// <summary>
    /// Write now, and keep writing while further changes arrive during the write, so the last
    /// change is never dropped. Never throws.
    /// </summary>
    public async Task FlushAsync()
    {
        await _writeGate.WaitAsync();

        try
        {
            while (_pendingWrite)
            {
                _pendingWrite = false;

                await WriteOnceAsync();
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteOnceAsync()
    {
        if (!_appState.HasPlan || !_disk.Status.CanWrite)
        {
            return;
        }

        PlanFileContent? content;

        try
        {
            content = await _planFiles.TryWriteWithoutPromptAsync(_appState.GetDocumentForSave());
        }
        catch (Exception ex)
        {
            // A plan that does not validate cannot be serialized at all. The working-copy path
            // reports this too; do not double-report it here.
            _ = ex;
            return;
        }

        if (content is null)
        {
            // Encrypted and locked. Leave the file alone and let the UI say it is behind rather
            // than throwing a passphrase prompt at someone who is mid-edit.
            SetBehind(true);
            return;
        }

        var result = await _disk.WriteAsync(content.Text);

        if (result.Ok)
        {
            LastWrittenAt = DateTimeOffset.UtcNow;
            _reportedPermissionLapse = false;

            SetBehind(false);

            return;
        }

        SetBehind(true);

        if (result.NeedsPermission)
        {
            if (_reportedPermissionLapse)
            {
                return;
            }

            _reportedPermissionLapse = true;
        }

        _feedback.Error(result.Message ?? "The linked file could not be written.");
    }

    private void SetBehind(bool behind)
    {
        if (FileIsBehind == behind)
        {
            return;
        }

        FileIsBehind = behind;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _appState.PlanChanged -= OnPlanChanged;

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _writeGate.Dispose();
    }
}
