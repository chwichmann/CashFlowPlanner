using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>How the link to a file on disk currently stands.</summary>
public enum DiskLinkState
{
    /// <summary>The browser has no File System Access API. Firefox and Safari, permanently.</summary>
    Unsupported = 0,

    /// <summary>Supported, but no file has been chosen.</summary>
    Unlinked = 1,

    /// <summary>A file is linked and writable. Autosave runs unattended.</summary>
    Granted = 2,

    /// <summary>
    /// A file is remembered but the grant lapsed. Re-granting needs a user gesture, so this
    /// is a button, never something the app can do for itself.
    /// </summary>
    NeedsPermission = 3
}

public sealed record DiskLinkStatus(DiskLinkState State, string? FileName)
{
    public bool CanWrite => State == DiskLinkState.Granted;
}

public sealed record DiskWriteResult(bool Ok, string Reason, string? Message, string? FileName)
{
    public bool NeedsPermission => Reason == "needs-permission";
}

/// <summary>
/// The part of disk autosave the coordinator depends on, extracted so the coordinator can be
/// tested without a browser - the same reason <see cref="IBrowserPlanCache"/> exists.
/// </summary>
public interface IDiskAutoSave
{
    event Action? StatusChanged;

    DiskLinkStatus Status { get; }

    Task<DiskWriteResult> WriteAsync(string text);
}

/// <summary>
/// Writes the plan straight to a file the user picked, and remembers which file between
/// sessions.
/// <para>
/// The browser working copy is not a backup - it dies with the site data, and it is invisible
/// from outside the browser. This is what makes the file on disk the thing that is actually
/// kept, without the user having to remember to export.
/// </para>
/// <para>
/// Because the file written here is already ciphertext when encryption is on, the folder can
/// safely be a synced one: OneDrive, iCloud or Dropbox then provide sync and off-site backup
/// without the provider ever seeing the contents.
/// </para>
/// </summary>
public sealed class DiskAutoSaveService : IDiskAutoSave, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public DiskAutoSaveService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Raised when the link state changes, so the UI can follow it.</summary>
    public event Action? StatusChanged;

    public DiskLinkStatus Status { get; private set; } = new(DiskLinkState.Unsupported, null);

    /// <summary>The last write that failed, for the UI to surface. Cleared by a successful write.</summary>
    public DiskWriteResult? LastFailure { get; private set; }

    private async Task<IJSObjectReference> ModuleAsync()
    {
        return _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/plan-file-handle.js");
    }

    private sealed record JsStatus(string State, string? FileName);

    /// <summary>
    /// The status the module reported, or "unsupported" when it reported nothing.
    ///
    /// <c>InvokeAsync&lt;JsStatus&gt;</c> answers null whenever the module resolves without a
    /// value - a browser without the File System Access API, an interop layer that stubs the
    /// call - and every call site here dereferenced it. That is a NullReferenceException out of
    /// OnInitializedAsync, which takes the whole Settings page down rather than degrading to the
    /// state the enum already has a name for.
    /// </summary>
    private static DiskLinkStatus ToStatus(JsStatus? status) =>
        status is null
            ? new DiskLinkStatus(DiskLinkState.Unsupported, null)
            : new DiskLinkStatus(Parse(status.State), status.FileName);

    private static DiskLinkState Parse(string state) => state switch
    {
        "granted" => DiskLinkState.Granted,
        "needs-permission" => DiskLinkState.NeedsPermission,
        "unlinked" => DiskLinkState.Unlinked,
        _ => DiskLinkState.Unsupported
    };

    /// <summary>Read the current state without prompting or touching the disk.</summary>
    public async Task RefreshAsync()
    {
        try
        {
            var module = await ModuleAsync();
            var status = await module.InvokeAsync<JsStatus?>("status");

            Status = ToStatus(status);
        }
        catch (JSException)
        {
            Status = new DiskLinkStatus(DiskLinkState.Unsupported, null);
        }

        StatusChanged?.Invoke();
    }

    /// <summary>Choose the file. Must be called from a user gesture.</summary>
    public async Task<bool> LinkAsync(string suggestedName)
    {
        var module = await ModuleAsync();
        var status = await module.InvokeAsync<JsStatus?>("link", suggestedName);

        Status = ToStatus(status);
        LastFailure = null;

        StatusChanged?.Invoke();

        return Status.CanWrite;
    }

    /// <summary>Re-grant access to the remembered file. Must be called from a user gesture.</summary>
    public async Task<bool> ReconnectAsync()
    {
        var module = await ModuleAsync();
        var status = await module.InvokeAsync<JsStatus?>("reconnect");

        Status = ToStatus(status);

        if (Status.CanWrite)
        {
            LastFailure = null;
        }

        StatusChanged?.Invoke();

        return Status.CanWrite;
    }

    public async Task UnlinkAsync()
    {
        var module = await ModuleAsync();
        var status = await module.InvokeAsync<JsStatus?>("unlink");

        Status = ToStatus(status);
        LastFailure = null;

        StatusChanged?.Invoke();
    }

    /// <summary>
    /// Write the plan to the linked file. Returns a result rather than throwing: this runs
    /// behind the user's back on a debounce, and a silently swallowed failure here would
    /// recreate exactly the class of bug the autosave work removed.
    /// </summary>
    public async Task<DiskWriteResult> WriteAsync(string text)
    {
        if (Status.State == DiskLinkState.Unsupported)
        {
            return new DiskWriteResult(false, "unsupported", null, null);
        }

        DiskWriteResult result;

        try
        {
            var module = await ModuleAsync();
            result = await module.InvokeAsync<DiskWriteResult>("write", text);
        }
        catch (JSException ex)
        {
            result = new DiskWriteResult(false, "failed", ex.Message, Status.FileName);
        }

        if (result.Ok)
        {
            LastFailure = null;

            if (Status.State != DiskLinkState.Granted)
            {
                Status = Status with { State = DiskLinkState.Granted };
                StatusChanged?.Invoke();
            }
        }
        else
        {
            LastFailure = result;

            if (result.NeedsPermission && Status.State == DiskLinkState.Granted)
            {
                // The grant lapsed mid-session; the UI needs to offer the reconnect button.
                Status = Status with { State = DiskLinkState.NeedsPermission };
            }

            StatusChanged?.Invoke();
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away.
            }
        }
    }
}
